#pragma warning disable SA1107, SA1119, SA1204, SA1214, SA1402, SA1501, SA1502, SA1503, SA1513, SA1516, SA1649
using System.Runtime.CompilerServices;
using System.Text.Json;
using DbTransfer.Core;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DbTransfer.Connectors.MongoDb;

public sealed class MongoDbSource : IDatabaseSource
{
    private readonly MongoClient client;
    private readonly IMongoCollection<BsonDocument> collection;
    private readonly FilterDefinition<BsonDocument> filter;
    private readonly int batchSize;
    private readonly long maxBatchBytes;
    private IAsyncCursor<BsonDocument>? cursor;
    private readonly Queue<BsonDocument> buffered = new();
    private RecordSchema? schema;

    public MongoDbSource(string connectionString, string query, int batchSize, long maxBatchBytes)
    {
        DocumentConnector.ValidateBatchLimits(batchSize, maxBatchBytes);
        var parsed = DocumentConnector.SplitQuery(query, "database/collection|{filter-json}");
        var location = DocumentConnector.SplitLocation(parsed.Location, "database/collection");
        client = new MongoClient(connectionString);
        collection = client.GetDatabase(location.First).GetCollection<BsonDocument>(location.Second);
        filter = string.IsNullOrWhiteSpace(parsed.Query) ? FilterDefinition<BsonDocument>.Empty : new BsonDocumentFilterDefinition<BsonDocument>(BsonDocument.Parse(parsed.Query));
        this.batchSize = batchSize;
        this.maxBatchBytes = maxBatchBytes;
    }

    public ConnectorCapabilities Capabilities => ConnectorCapabilities.None;
    public async ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken)
    {
        if (schema is not null) return schema;
        cursor = await collection.FindAsync(filter, new FindOptions<BsonDocument> { BatchSize = batchSize }, cancellationToken).ConfigureAwait(false);
        if (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false)) foreach (var item in cursor!.Current) buffered.Enqueue(item);
        schema = buffered.Count == 0 ? new RecordSchema([new RecordColumn("_id", typeof(object))])
            : new RecordSchema(buffered.Peek().Names.Select(name => new RecordColumn(name, typeof(object))));
        return schema;
    }

    public async IAsyncEnumerable<RecordBatch> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var currentSchema = await GetSchemaAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<object?[]>(batchSize); long bytes = 0;
        while (buffered.Count > 0 || await cursor!.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            if (buffered.Count == 0) foreach (var item in cursor!.Current) buffered.Enqueue(item);
            while (buffered.TryDequeue(out var document))
            {
                var row = currentSchema.Columns.Select(c => document.TryGetValue(c.Name, out var value) ? ToValue(value) : null).ToArray();
                var size = DocumentConnector.Estimate(row);
                if (size > maxBatchBytes) throw new InvalidOperationException("A source row exceeds the configured maximum batch bytes.");
                if (rows.Count > 0 && bytes + size > maxBatchBytes) { yield return new RecordBatch(currentSchema, rows, bytes); rows.Clear(); bytes = 0; }
                rows.Add(row); bytes += size;
                if (rows.Count >= batchSize) { yield return new RecordBatch(currentSchema, rows, bytes); rows.Clear(); bytes = 0; }
            }
        }
        if (rows.Count > 0) yield return new RecordBatch(currentSchema, rows, bytes);
    }

    public ValueTask DisposeAsync() { cursor?.Dispose(); return ValueTask.CompletedTask; }
    private static object? ToValue(BsonValue value) => value.BsonType switch
    {
        BsonType.Null => null,
        BsonType.ObjectId => value.AsObjectId.ToString(),
        BsonType.Document or BsonType.Array => value.ToJson(),
        _ => BsonTypeMapper.MapToDotNetValue(value),
    };
}

public sealed class MongoDbSink(string connectionString) : IDatabaseSink
{
    private IMongoCollection<BsonDocument>? collection;
    private RecordSchema? schema;
    private string[] names = [];
    public ConnectorCapabilities Capabilities => ConnectorCapabilities.NativeBulk | ConnectorCapabilities.OrderedWrites;
    public async ValueTask InitializeAsync(RecordSchema sourceSchema, DatabaseTransferOptions options, CancellationToken cancellationToken)
    {
        if (options.TransactionMode != TransactionMode.None) throw new NotSupportedException("MongoDB requires --transaction none; cross-document transaction semantics are not implied by bulk insertion.");
        var database = options.Destination.Schema ?? options.Destination.Catalog ?? new MongoUrl(connectionString).DatabaseName
            ?? throw new ArgumentException("MongoDB destination must be database.collection or the connection URI must name a database.");
        var db = new MongoClient(connectionString).GetDatabase(database);
        if (options.CreateTable)
        {
            var exists = await (await db.ListCollectionNamesAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).ToListAsync(cancellationToken).ConfigureAwait(false);
            if (!exists.Contains(options.Destination.Name, StringComparer.Ordinal)) await db.CreateCollectionAsync(options.Destination.Name, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        collection = db.GetCollection<BsonDocument>(options.Destination.Name);
        schema = sourceSchema;
        names = sourceSchema.Columns.Select(c => options.ColumnMappings.TryGetValue(c.Name, out var n) ? n : c.Name).ToArray();
    }
    public ValueTask ValidateAsync(RecordSchema sourceSchema, CancellationToken cancellationToken)
    {
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw new ArgumentException("Column mappings must be unique.");
        return ValueTask.CompletedTask;
    }
    public async ValueTask<WriteResult> WriteAsync(RecordBatch batch, CancellationToken cancellationToken)
    {
        var documents = batch.Rows.Select(row => new BsonDocument(names.Select((name, i) => new BsonElement(name, ToBson(row[i])))));
        await collection!.InsertManyAsync(documents, new InsertManyOptions { IsOrdered = true }, cancellationToken).ConfigureAwait(false);
        return WriteResult.Success(batch.Count);
    }
    public ValueTask CompleteAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    private static BsonValue ToBson(object? value) => value switch
    {
        null => BsonNull.Value,
        DateTimeOffset offset => new BsonDateTime(offset.UtcDateTime),
        JsonElement json => BsonDocument.Parse($"{{\"v\":{json.GetRawText()}}}")["v"],
        string text when (text.StartsWith('{') || text.StartsWith('[')) => BsonDocument.Parse($"{{\"v\":{text}}}")["v"],
        _ => BsonValue.Create(value),
    };
}
