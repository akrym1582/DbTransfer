using Azure.Data.Tables;
using DbTransfer.Connectors.AzureTableStorage;
using DbTransfer.Core;

namespace DbTransfer.Tests;

public sealed class AzureTableStorageIntegrationTests
{
    [Fact]
    [Trait("Category", "AzureTableStorageIntegration")]
    public async Task AzureTableStorage_sink_and_source_round_trip_entities()
    {
        var connectionString = Environment.GetEnvironmentVariable("DBTRANSFER_AZURITE_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var tableName = $"integration{Guid.NewGuid():N}";
        var schema = new RecordSchema(
        [
            new("PartitionKey", typeof(string), false),
            new("RowKey", typeof(string), false),
            new("Value", typeof(string)),
        ]);
        await using var sink = new AzureTableStorageSink(connectionString);
        try
        {
            await sink.InitializeAsync(
                schema,
                new DatabaseTransferOptions
                {
                    Destination = new QualifiedName(tableName),
                    CreateTable = true,
                    TransactionMode = TransactionMode.None,
                },
                default);
            await sink.ValidateAsync(schema, default);
            var result = await sink.WriteAsync(
                new RecordBatch(schema, [["partition", "one", "alpha"], ["partition", "two", null]], 64),
                default);
            Assert.Equal(2, result.Succeeded);

            await using var source = new AzureTableStorageSource(
                connectionString,
                $"{tableName}|PartitionKey eq 'partition'",
                1,
                1024);
            var sourceSchema = await source.GetSchemaAsync(default);
            var rows = new List<object?[]>();
            await foreach (var batch in source.ReadAsync(default))
            {
                rows.AddRange(batch.Rows);
            }

            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.Equal("partition", row[sourceSchema.GetOrdinal("PartitionKey")]));
            Assert.Contains(rows, row => Equals(row[sourceSchema.GetOrdinal("RowKey")], "one") && Equals(row[sourceSchema.GetOrdinal("Value")], "alpha"));
            Assert.Contains(rows, row => Equals(row[sourceSchema.GetOrdinal("RowKey")], "two") && row[sourceSchema.GetOrdinal("Value")] is null);
        }
        finally
        {
            await new TableClient(connectionString, tableName).DeleteAsync();
        }
    }
}
