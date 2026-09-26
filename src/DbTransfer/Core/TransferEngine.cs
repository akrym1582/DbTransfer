using System.Threading.Channels;

namespace DbTransfer.Core;

public sealed record TransferOptions
{
    public int BufferBatches { get; init; } = 2;
    public long MemoryBudgetBytes { get; init; } = 64 * 1024 * 1024;

    public void Validate()
    {
        if (BufferBatches < 1) throw new ArgumentOutOfRangeException(nameof(BufferBatches));
        if (MemoryBudgetBytes < 1) throw new ArgumentOutOfRangeException(nameof(MemoryBudgetBytes));
    }
}

public sealed record TransferResult(long Read, long Written, WriteStatus Status);

public sealed class TransferEngine
{
    public async Task<TransferResult> RunAsync(
        ISourceConnector source, ISinkConnector sink, TransferOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        using var transferCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var transferToken = transferCancellation.Token;
        var schema = await source.GetSchemaAsync(transferToken).ConfigureAwait(false);
        await sink.ValidateAsync(schema, transferToken).ConfigureAwait(false);

        var channel = Channel.CreateBounded<RecordBatch>(new BoundedChannelOptions(options.BufferBatches)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
        using var budget = new SemaphoreSlim(1, 1);
        long bufferedBytes = 0;
        long read = 0, written = 0;

        var producer = Task.Run(async () =>
        {
            try
            {
                await foreach (var batch in source.ReadAsync(transferToken).WithCancellation(transferToken))
                {
                    if (batch.EstimatedBytes > options.MemoryBudgetBytes)
                        throw new InvalidOperationException("A single batch exceeds the configured memory budget.");
                    while (true)
                    {
                        await budget.WaitAsync(transferToken).ConfigureAwait(false);
                        try
                        {
                            if (bufferedBytes + batch.EstimatedBytes <= options.MemoryBudgetBytes)
                            { bufferedBytes += batch.EstimatedBytes; break; }
                        }
                        finally { budget.Release(); }
                        await Task.Delay(10, transferToken).ConfigureAwait(false);
                    }
                    read += batch.Count;
                    await channel.Writer.WriteAsync(batch, transferToken).ConfigureAwait(false);
                }
                channel.Writer.TryComplete();
            }
            catch (Exception ex) { channel.Writer.TryComplete(ex); throw; }
        }, transferToken);

        try
        {
            await foreach (var batch in channel.Reader.ReadAllAsync(transferToken))
            {
                var result = await sink.WriteAsync(batch, transferToken).ConfigureAwait(false);
                if (result.Status != WriteStatus.Succeeded)
                    return new TransferResult(read, written + result.Succeeded, result.Status);
                written += result.Succeeded;
                await budget.WaitAsync(transferToken).ConfigureAwait(false);
                try { bufferedBytes -= batch.EstimatedBytes; }
                finally { budget.Release(); }
            }
            await producer.ConfigureAwait(false);
            return new TransferResult(read, written, WriteStatus.Succeeded);
        }
        finally
        {
            if (!producer.IsCompleted)
            {
                transferCancellation.Cancel();
                channel.Writer.TryComplete(new OperationCanceledException());
                try { await producer.ConfigureAwait(false); } catch { /* preserve the consumer failure */ }
            }
        }
    }
}
