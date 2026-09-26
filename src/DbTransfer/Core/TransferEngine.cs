using System.Threading.Channels;

namespace DbTransfer.Core;

/// <summary>有限のバッチキューとバイト予算を使い、読み取りが書き込みを無制限に追い越さない転送パイプラインを実行します。</summary>
public sealed class TransferEngine
{
    /// <summary>スキーマを事前検証した後、接続元のバッチを順次読み取り、接続先へ書き込みます。</summary>
    /// <param name="source">スキーマとレコードをストリーミングする接続元です。</param>
    /// <param name="sink">スキーマを検証し、各バッチを書き込む転送先です。</param>
    /// <param name="options">バッファーのバッチ数と推定バイト数の上限です。</param>
    /// <param name="cancellationToken">スキーマ取得、読み取り、および書き込みをまとめて中止する要求を受け取るトークンです。</param>
    /// <returns>読み取り済み件数、書き込み確認済み件数、および最終状態です。</returns>
    /// <exception cref="ArgumentNullException">引数のいずれかが <see langword="null"/> の場合に発生します。</exception>
    /// <exception cref="ArgumentOutOfRangeException">バッファー設定が 1 未満の場合に発生します。</exception>
    /// <exception cref="InvalidOperationException">一つのバッチだけでバイト予算を超える場合に発生します。</exception>
    /// <exception cref="OperationCanceledException">転送の中止が要求された場合に発生します。</exception>
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

        var producer = Task.Run(
            async () =>
        {
            try
            {
                await foreach (var batch in source.ReadAsync(transferToken).WithCancellation(transferToken))
                {
                    if (batch.EstimatedBytes > options.MemoryBudgetBytes)
                    {
                        throw new InvalidOperationException("A single batch exceeds the configured memory budget.");
                    }

                    while (true)
                    {
                        await budget.WaitAsync(transferToken).ConfigureAwait(false);
                        try
                        {
                            if (bufferedBytes + batch.EstimatedBytes <= options.MemoryBudgetBytes)
                            {
                                bufferedBytes += batch.EstimatedBytes;
                                break;
                            }
                        }
                        finally
                        {
                            budget.Release();
                        }

                        await Task.Delay(10, transferToken).ConfigureAwait(false);
                    }

                    read += batch.Count;
                    await channel.Writer.WriteAsync(batch, transferToken).ConfigureAwait(false);
                }

                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
                throw;
            }
        },
            transferToken);

        try
        {
            await foreach (var batch in channel.Reader.ReadAllAsync(transferToken))
            {
                var result = await sink.WriteAsync(batch, transferToken).ConfigureAwait(false);
                if (result.Status != WriteStatus.Succeeded)
                {
                    return new TransferResult(read, written + result.Succeeded, result.Status);
                }

                written += result.Succeeded;
                options.Progress?.Invoke(read, written);
                await budget.WaitAsync(transferToken).ConfigureAwait(false);
                try
                {
                    bufferedBytes -= batch.EstimatedBytes;
                }
                finally
                {
                    budget.Release();
                }
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
                try
                {
                    await producer.ConfigureAwait(false);
                }
                catch
                { /* preserve the consumer failure */
                }
            }
        }
    }
}
