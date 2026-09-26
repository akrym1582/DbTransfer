namespace DbTransfer.Core;

public interface IDatabaseSink : ISinkConnector, IAsyncDisposable
{
    ValueTask InitializeAsync(RecordSchema schema, DatabaseTransferOptions options, CancellationToken cancellationToken);

    ValueTask CompleteAsync(CancellationToken cancellationToken);
}
