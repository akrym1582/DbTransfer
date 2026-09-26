using DbTransfer.Connectors.SqlServer;
using DbTransfer.Core;
using Microsoft.Data.SqlClient;

namespace DbTransfer.Tests;

public sealed class SqlServerIntegrationTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SqlServer_copy_round_trips_rows()
    {
        var connectionString = Environment.GetEnvironmentVariable("DBTRANSFER_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var table = $"integration_{Guid.NewGuid():N}";
        await using var source = new DatabaseSourceConnector(
            SqlClientFactory.Instance,
            connectionString,
            "SELECT id, value FROM (VALUES (1, N'alpha'), (2, NULL), (3, N'日本語')) source(id, value) ORDER BY id",
            2,
            1024);
        await using var sink = new SqlServerConnector(connectionString);
        try
        {
            var result = await new DatabaseTransferRunner().RunAsync(
                source,
                sink,
                new DatabaseTransferOptions
                {
                    Destination = new QualifiedName(table, "dbo"),
                    CreateTable = true,
                    TransactionMode = TransactionMode.Batch,
                },
                new TransferOptions { BufferBatches = 1, MemoryBudgetBytes = 2048 });

            Assert.Equal(3, result.Written);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand($"SELECT value FROM dbo.[{table}] ORDER BY id", connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("alpha", reader.GetString(0));
            Assert.True(await reader.ReadAsync());
            Assert.True(await reader.IsDBNullAsync(0));
            Assert.True(await reader.ReadAsync());
            Assert.Equal("日本語", reader.GetString(0));
        }
        finally
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await new SqlCommand($"DROP TABLE IF EXISTS dbo.[{table}]", connection).ExecuteNonQueryAsync();
        }
    }
}
