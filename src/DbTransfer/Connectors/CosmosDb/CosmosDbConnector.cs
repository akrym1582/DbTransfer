#pragma warning disable SA1107, SA1119, SA1204, SA1214, SA1402, SA1501, SA1502, SA1503, SA1513, SA1516, SA1649
using System.Runtime.CompilerServices;
using System.Text.Json;
using DbTransfer.Core;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

namespace DbTransfer.Connectors.CosmosDb;

public sealed class CosmosDbSource : IDatabaseSource
{
    private readonly CosmosClient client;
    private readonly Container container;
    private readonly FeedIterator<JObject> iterator;
    private readonly int batchSize;
    private readonly long maxBatchBytes;
    private readonly Queue<JObject> buffered = new();
    private RecordSchema? schema;

    public CosmosDbSource(string connectionString, string query, int batchSize, long maxBatchBytes)
    {
        DocumentConnector.ValidateBatchLimits(batchSize, maxBatchBytes);
        var parsed = DocumentConnector.SplitQuery(query, "database/container|SELECT ...");
        var location = DocumentConnector.SplitLocation(parsed.Location, "database/container");
        client = new CosmosClient(connectionString);
        container = client.GetContainer(location.First, location.Second);
        iterator = container.GetItemQueryIterator<JObject>(new QueryDefinition(parsed.Query));
        this.batchSize = batchSize;
        this.maxBatchBytes = maxBatchBytes;
    }

    public ConnectorCapabilities Capabilities => ConnectorCapabilities.None;

    public async ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken)
    {
        if (schema is not null) return schema;
        if (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false)) buffered.Enqueue(item);
        }

        schema = buffered.Count == 0
            ? new RecordSchema([new RecordColumn("id", typeof(string))])
            : new RecordSchema(buffered.Peek().Properties().Select(p => new RecordColumn(p.Name, typeof(object))));
        return schema;
    }

    public async IAsyncEnumerable<RecordBatch> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var currentSchema = await GetSchemaAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<object?[]>(batchSize);
        long bytes = 0;
        while (buffered.Count > 0 || iterator.HasMoreResults)
        {
            if (buffered.Count == 0)
                foreach (var item in await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false)) buffered.Enqueue(item);
            while (buffered.TryDequeue(out var document))
            {
                using var json = JsonDocument.Parse(document.ToString());
                var properties = json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
                var row = currentSchema.Columns.Select(c => properties.TryGetValue(c.Name, out var v) ? DocumentConnector.JsonValue(v) : null).ToArray();
                var size = DocumentConnector.Estimate(row);
                if (size > maxBatchBytes) throw new InvalidOperationException("A source row exceeds the configured maximum batch bytes.");
                if (rows.Count > 0 && bytes + size > maxBatchBytes) { yield return new RecordBatch(currentSchema, rows, bytes); rows.Clear(); bytes = 0; }
                rows.Add(row); bytes += size;
                if (rows.Count >= batchSize) { yield return new RecordBatch(currentSchema, rows, bytes); rows.Clear(); bytes = 0; }
            }
        }
        if (rows.Count > 0) yield return new RecordBatch(currentSchema, rows, bytes);
    }

    public ValueTask DisposeAsync() { client.Dispose(); return ValueTask.CompletedTask; }
}

public sealed class CosmosDbSink(string connectionString) : IDatabaseSink
{
    private CosmosClient? client;
    private Container? container;
    private RecordSchema? schema;
    private string[] names = [];
    public ConnectorCapabilities Capabilities => ConnectorCapabilities.NativeBulk;

    public async ValueTask InitializeAsync(RecordSchema sourceSchema, DatabaseTransferOptions options, CancellationToken cancellationToken)
    {
        RejectTransactions(options);
        var database = options.Destination.Schema ?? options.Destination.Catalog ?? throw new ArgumentException("Cosmos DB destination must be database.container.");
        client = new CosmosClient(connectionString, new CosmosClientOptions { AllowBulkExecution = true });
        if (options.CreateTable) await client.CreateDatabaseIfNotExistsAsync(database, cancellationToken: cancellationToken).ConfigureAwait(false);
        var db = client.GetDatabase(database);
        container = options.CreateTable
            ? (await db.CreateContainerIfNotExistsAsync(options.Destination.Name, "/id", cancellationToken: cancellationToken).ConfigureAwait(false)).Container
            : db.GetContainer(options.Destination.Name);
        schema = sourceSchema;
        names = sourceSchema.Columns.Select(c => options.ColumnMappings.TryGetValue(c.Name, out var n) ? n : c.Name).ToArray();
    }

    public ValueTask ValidateAsync(RecordSchema sourceSchema, CancellationToken cancellationToken)
    {
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw new ArgumentException("Column mappings must be unique.");
        if (!names.Contains("id", StringComparer.Ordinal)) throw new ArgumentException("Cosmos DB records require an 'id' property (map a source column to id).");
        return ValueTask.CompletedTask;
    }

    public async ValueTask<WriteResult> WriteAsync(RecordBatch batch, CancellationToken cancellationToken)
    {
        var tasks = batch.Rows.Select(row => container!.CreateItemAsync(DocumentConnector.Row(schema!, names, row), cancellationToken: cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return WriteResult.Success(batch.Count);
    }
    public ValueTask CompleteAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() { client?.Dispose(); return ValueTask.CompletedTask; }
    private static void RejectTransactions(DatabaseTransferOptions options) { if (options.TransactionMode != TransactionMode.None) throw new NotSupportedException("Cosmos DB requires --transaction none because records may span partition keys."); }
}
