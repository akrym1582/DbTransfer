using System.Runtime.CompilerServices;

namespace DbTransfer.Core;

/// <summary>Composes database connectors with mapping, transactions, and durable batch checkpoints.</summary>
public sealed class DatabaseTransferRunner
{
    public async Task<TransferResult> RunAsync(
        ISourceConnector source,
        IDatabaseSink sink,
        DatabaseTransferOptions databaseOptions,
        TransferOptions transferOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(databaseOptions);
        if (databaseOptions.Resume && string.IsNullOrWhiteSpace(databaseOptions.CheckpointFile))
        {
            throw new ArgumentException("Resume requires a checkpoint file.", nameof(databaseOptions));
        }

        if (databaseOptions.Resume && databaseOptions.TransactionMode == TransactionMode.All)
        {
            throw new NotSupportedException("Resume cannot be combined with a whole-transfer transaction.");
        }

        var schema = await source.GetSchemaAsync(cancellationToken).ConfigureAwait(false);
        await sink.InitializeAsync(schema, databaseOptions, cancellationToken).ConfigureAwait(false);
        CheckpointStore? store = string.IsNullOrWhiteSpace(databaseOptions.CheckpointFile) ? null : new(databaseOptions.CheckpointFile);
        var checkpoint = databaseOptions.Resume && store is not null
            ? await store.LoadAsync(cancellationToken).ConfigureAwait(false) : null;
        var startBatch = checkpoint?.BatchesCompleted ?? 0;
        var startRows = checkpoint?.RowsCompleted ?? 0;
        var sourceView = new SkippingSource(source, schema, startBatch);
        ISinkConnector sinkView = store is null ? sink : new CheckpointingSink(sink, store, startBatch, startRows);
        var result = await new TransferEngine().RunAsync(sourceView, sinkView, transferOptions, cancellationToken).ConfigureAwait(false);
        if (result.Status == WriteStatus.Succeeded)
        {
            await sink.CompleteAsync(cancellationToken).ConfigureAwait(false);
            store?.Delete();
        }

        return result with { Read = result.Read + startRows, Written = result.Written + startRows };
    }

    private sealed class SkippingSource(ISourceConnector inner, RecordSchema schema, long skip) : ISourceConnector
    {
        public ConnectorCapabilities Capabilities => inner.Capabilities;

        public ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(schema);

        public async IAsyncEnumerable<RecordBatch> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            long index = 0;
            await foreach (var batch in inner.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (index++ >= skip)
                {
                    yield return batch;
                }
            }
        }
    }

    private sealed class CheckpointingSink(IDatabaseSink inner, CheckpointStore store, long batches, long rows) : ISinkConnector
    {
        public ConnectorCapabilities Capabilities => inner.Capabilities | ConnectorCapabilities.Resume;

        public ValueTask ValidateAsync(RecordSchema schema, CancellationToken cancellationToken) => inner.ValidateAsync(schema, cancellationToken);

        public async ValueTask<WriteResult> WriteAsync(RecordBatch batch, CancellationToken cancellationToken)
        {
            var result = await inner.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
            if (result.Status == WriteStatus.Succeeded)
            {
                batches++;
                rows += result.Succeeded;
                await store.SaveAsync(new TransferCheckpoint(batches, rows, result.Continuation), cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
    }
}
