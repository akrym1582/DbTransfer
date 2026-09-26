#pragma warning disable SA1107, SA1119, SA1204, SA1214, SA1402, SA1501, SA1502, SA1503, SA1513, SA1516, SA1649
using System.Runtime.CompilerServices;
using Azure;
using Azure.Data.Tables;
using DbTransfer.Core;

namespace DbTransfer.Connectors.AzureTableStorage;

public sealed class AzureTableStorageSource : IDatabaseSource
{
    private readonly TableClient table;
    private readonly string? filter;
    private readonly int batchSize;
    private readonly long maxBatchBytes;
    private IAsyncEnumerator<TableEntity>? enumerator;
    private TableEntity? first;
    private RecordSchema? schema;

    public AzureTableStorageSource(string connectionString, string query, int batchSize, long maxBatchBytes)
    {
        DocumentConnector.ValidateBatchLimits(batchSize, maxBatchBytes);
        var parsed = DocumentConnector.SplitQuery(query, "table|OData filter");
        table = new TableClient(connectionString, parsed.Location);
        filter = string.IsNullOrWhiteSpace(parsed.Query) ? null : parsed.Query;
        this.batchSize = batchSize;
        this.maxBatchBytes = maxBatchBytes;
    }
    public ConnectorCapabilities Capabilities => ConnectorCapabilities.None;
    public async ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken)
    {
        if (schema is not null) return schema;
        enumerator = table.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        if (await enumerator.MoveNextAsync().ConfigureAwait(false)) first = enumerator!.Current;
        var names = first is null
            ? ["PartitionKey", "RowKey"]
            : new[] { "PartitionKey", "RowKey" }.Concat(first.Keys.Where(name => name is not "PartitionKey" and not "RowKey")).ToArray();
        schema = new RecordSchema(names.Select(name => new RecordColumn(name, typeof(object))));
        return schema;
    }
    public async IAsyncEnumerable<RecordBatch> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var currentSchema = await GetSchemaAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<object?[]>(batchSize); long bytes = 0;
        while (first is not null || await enumerator!.MoveNextAsync().ConfigureAwait(false))
        {
            var entity = first ?? enumerator!.Current; first = null;
            var row = currentSchema.Columns.Select(c => c.Name switch
            {
                "PartitionKey" => entity.PartitionKey,
                "RowKey" => entity.RowKey,
                _ => entity.TryGetValue(c.Name, out var value) ? value : null,
            }).ToArray();
            var size = DocumentConnector.Estimate(row);
            if (size > maxBatchBytes) throw new InvalidOperationException("A source row exceeds the configured maximum batch bytes.");
            if (rows.Count > 0 && bytes + size > maxBatchBytes) { yield return new RecordBatch(currentSchema, rows, bytes); rows.Clear(); bytes = 0; }
            rows.Add(row); bytes += size;
            if (rows.Count >= batchSize) { yield return new RecordBatch(currentSchema, rows, bytes); rows.Clear(); bytes = 0; }
        }
        if (rows.Count > 0) yield return new RecordBatch(currentSchema, rows, bytes);
    }
    public async ValueTask DisposeAsync() { if (enumerator is not null) await enumerator.DisposeAsync().ConfigureAwait(false); }
}

public sealed class AzureTableStorageSink(string connectionString) : IDatabaseSink
{
    private TableClient? table;
    private RecordSchema? schema;
    private string[] names = [];
    public ConnectorCapabilities Capabilities => ConnectorCapabilities.NativeBulk;
    public async ValueTask InitializeAsync(RecordSchema sourceSchema, DatabaseTransferOptions options, CancellationToken cancellationToken)
    {
        if (options.TransactionMode != TransactionMode.None) throw new NotSupportedException("Azure Table Storage requires --transaction none because a transfer may span partitions.");
        if (options.Destination.Schema is not null || options.Destination.Catalog is not null) throw new ArgumentException("Azure Table Storage destination must be a one-part table name.");
        table = new TableClient(connectionString, options.Destination.Name);
        if (options.CreateTable) await table.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        schema = sourceSchema;
        names = sourceSchema.Columns.Select(c => options.ColumnMappings.TryGetValue(c.Name, out var n) ? n : c.Name).ToArray();
    }
    public ValueTask ValidateAsync(RecordSchema sourceSchema, CancellationToken cancellationToken)
    {
        if (!names.Contains("PartitionKey", StringComparer.Ordinal) || !names.Contains("RowKey", StringComparer.Ordinal))
            throw new ArgumentException("Azure Table Storage records require PartitionKey and RowKey columns.");
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw new ArgumentException("Column mappings must be unique.");
        return ValueTask.CompletedTask;
    }
    public async ValueTask<WriteResult> WriteAsync(RecordBatch batch, CancellationToken cancellationToken)
    {
        var entities = batch.Rows.Select(ToEntity).ToArray();
        foreach (var partition in entities.GroupBy(entity => entity.PartitionKey, StringComparer.Ordinal))
        {
            foreach (var chunk in partition.Chunk(100))
            {
                var actions = chunk.Select(entity => new TableTransactionAction(TableTransactionActionType.Add, entity));
                await table!.SubmitTransactionAsync(actions, cancellationToken).ConfigureAwait(false);
            }
        }
        return WriteResult.Success(batch.Count);
    }
    public ValueTask CompleteAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    private TableEntity ToEntity(object?[] row)
    {
        var values = DocumentConnector.Row(schema!, names, row);
        var entity = new TableEntity(Convert.ToString(values["PartitionKey"], System.Globalization.CultureInfo.InvariantCulture)!, Convert.ToString(values["RowKey"], System.Globalization.CultureInfo.InvariantCulture)!);
        foreach (var pair in values.Where(pair => pair.Key is not "PartitionKey" and not "RowKey" and not "Timestamp" and not "odata.etag")) entity[pair.Key] = Normalize(pair.Value);
        return entity;
    }
    private static object? Normalize(object? value) => value switch { decimal number => (double)number, DateTimeOffset => value, DateTime => value, Guid => value, byte[] => value, bool => value, int => value, long => value, double => value, string => value, null => null, _ => value.ToString() };
}
