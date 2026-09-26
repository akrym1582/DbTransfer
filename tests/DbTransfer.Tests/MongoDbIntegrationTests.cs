using DbTransfer.Connectors.MongoDb;
using DbTransfer.Core;
using MongoDB.Driver;

namespace DbTransfer.Tests;

public sealed class MongoDbIntegrationTests
{
    [Fact]
    [Trait("Category", "MongoDbIntegration")]
    public async Task MongoDb_sink_and_source_round_trip_documents()
    {
        var connectionString = Environment.GetEnvironmentVariable("DBTRANSFER_MONGODB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var database = $"dbtransfer_{Guid.NewGuid():N}";
        var collection = "records";
        var schema = new RecordSchema([new("id", typeof(string), false), new("value", typeof(string))]);
        await using var sink = new MongoDbSink(connectionString);
        try
        {
            await sink.InitializeAsync(
                schema,
                new DatabaseTransferOptions
                {
                    Destination = new QualifiedName(collection, database),
                    CreateTable = true,
                    TransactionMode = TransactionMode.None,
                },
                default);
            await sink.ValidateAsync(schema, default);
            var result = await sink.WriteAsync(new RecordBatch(schema, [["one", "alpha"], ["two", null]], 32), default);
            Assert.Equal(2, result.Succeeded);

            await using var source = new MongoDbSource(connectionString, $"{database}/{collection}|{{}}", 1, 1024);
            var sourceSchema = await source.GetSchemaAsync(default);
            var rows = new List<object?[]>();
            await foreach (var batch in source.ReadAsync(default))
            {
                rows.AddRange(batch.Rows);
            }

            Assert.Equal(2, rows.Count);
            var id = sourceSchema.GetOrdinal("id");
            var value = sourceSchema.GetOrdinal("value");
            Assert.Contains(rows, row => Equals(row[id], "one") && Equals(row[value], "alpha"));
            Assert.Contains(rows, row => Equals(row[id], "two") && row[value] is null);
        }
        finally
        {
            await new MongoClient(connectionString).DropDatabaseAsync(database);
        }
    }
}
