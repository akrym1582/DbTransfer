namespace DbTransfer.Core;

[Flags]
public enum ConnectorCapabilities
{
    None = 0, NativeBulk = 1, Upsert = 2, BatchTransactions = 4,
    AllTransaction = 8, PartialSuccess = 16, Resume = 32, OrderedWrites = 64
}

public enum WriteStatus { Succeeded, Failed, Unknown }

public sealed record WriteResult(WriteStatus Status, long Succeeded, long Failed = 0, string? Continuation = null)
{
    public static WriteResult Success(long count, string? continuation = null) => new(WriteStatus.Succeeded, count, 0, continuation);
}

public interface ISourceConnector
{
    ConnectorCapabilities Capabilities { get; }
    ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<RecordBatch> ReadAsync(CancellationToken cancellationToken);
}

public interface ISinkConnector
{
    ConnectorCapabilities Capabilities { get; }
    ValueTask ValidateAsync(RecordSchema schema, CancellationToken cancellationToken);
    ValueTask<WriteResult> WriteAsync(RecordBatch batch, CancellationToken cancellationToken);
}
