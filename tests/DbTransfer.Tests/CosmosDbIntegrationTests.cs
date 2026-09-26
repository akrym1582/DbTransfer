using DbTransfer.Connectors.CosmosDb;
using DbTransfer.Core;
using Microsoft.Azure.Cosmos;

namespace DbTransfer.Tests;

public sealed class CosmosDbIntegrationTests
{
    [Fact]
    [Trait("Category", "CosmosDbIntegration")]
    public async Task CosmosDb_sink_and_source_round_trip_documents()
    {
        var connectionString = Environment.GetEnvironmentVariable("DBTRANSFER_COSMOSDB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var database = $"dbtransfer-{Guid.NewGuid():N}";
        var container = "records";
        var schema = new RecordSchema([new("id", typeof(string), false), new("value", typeof(string))]);
        await using var sink = new CosmosDbSink(connectionString);
        try
        {
            await sink.InitializeAsync(
                schema,
                new DatabaseTransferOptions
                {
                    Destination = new QualifiedName(container, database),
                    CreateTable = true,
                    TransactionMode = TransactionMode.None,
                },
                default);
            await sink.ValidateAsync(schema, default);
            var result = await sink.WriteAsync(new RecordBatch(schema, [["one", "alpha"], ["two", null]], 32), default);
            Assert.Equal(2, result.Succeeded);

            await using var source = new CosmosDbSource(
                connectionString,
                $"{database}/{container}|SELECT c.id, c[\"value\"] FROM c ORDER BY c.id",
                1,
                1024);
            var sourceSchema = await source.GetSchemaAsync(default);
            var rows = new List<object?[]>();
            await foreach (var batch in source.ReadAsync(default))
            {
                rows.AddRange(batch.Rows);
            }

            Assert.Equal(2, rows.Count);
            var id = sourceSchema.GetOrdinal("id");
            var value = sourceSchema.GetOrdinal("value");
            Assert.Equal("one", rows[0][id]);
            Assert.Equal("alpha", rows[0][value]);
            Assert.Equal("two", rows[1][id]);
            Assert.Null(rows[1][value]);
        }
        finally
        {
            using var client = new CosmosClient(connectionString);
            await client.GetDatabase(database).DeleteAsync();
        }
    }
}
