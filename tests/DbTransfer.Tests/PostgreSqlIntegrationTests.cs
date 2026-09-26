using System.Text;
using DbTransfer.Connectors.PostgreSql;
using DbTransfer.Core;
using DbTransfer.Formats;
using Npgsql;

namespace DbTransfer.Tests;

/// <summary>Exercises the streaming transfer pipeline against a live PostgreSQL instance.</summary>
public sealed class PostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "DBTRANSFER_POSTGRES_CONNECTION";

    /// <summary>Copies multiple batches through PostgreSQL binary COPY and preserves values.</summary>
    /// <returns>A task that completes after the copied rows have been verified.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgreSql_to_PostgreSql_copy_round_trips_rows()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var tableName = $"integration_{Guid.NewGuid():N}";
        await using var source = new DatabaseSourceConnector(
            NpgsqlFactory.Instance,
            connectionString,
            "SELECT id, value FROM (VALUES (1, 'alpha'::text), (2, NULL::text), (3, '日本語'::text)) AS source(id, value) ORDER BY id",
            batchSize: 2,
            maxBatchBytes: 1024);
        await using var sink = new PostgreSqlConnector(connectionString);

        try
        {
            var result = await new DatabaseTransferRunner().RunAsync(
                source,
                sink,
                new DatabaseTransferOptions
                {
                    Destination = new QualifiedName(tableName, "public"),
                    CreateTable = true,
                    TransactionMode = TransactionMode.Batch,
                    UseNativeBulk = true,
                },
                new TransferOptions { BufferBatches = 1, MemoryBudgetBytes = 2048 });

            Assert.Equal(WriteStatus.Succeeded, result.Status);
            Assert.Equal(3, result.Read);
            Assert.Equal(3, result.Written);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"SELECT id, value FROM public.\"{tableName}\" ORDER BY id",
                connection);
            await using var reader = await command.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal(1, reader.GetInt32(0));
            Assert.Equal("alpha", reader.GetString(1));
            Assert.True(await reader.ReadAsync());
            Assert.Equal(2, reader.GetInt32(0));
            Assert.True(await reader.IsDBNullAsync(1));
            Assert.True(await reader.ReadAsync());
            Assert.Equal(3, reader.GetInt32(0));
            Assert.Equal("日本語", reader.GetString(1));
            Assert.False(await reader.ReadAsync());
        }
        finally
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP TABLE IF EXISTS public.\"{tableName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Json_import_and_csv_export_round_trip_through_PostgreSql()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var tableName = $"format_integration_{Guid.NewGuid():N}";
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("[{\"id\":1,\"value\":\"alpha\"},{\"id\":2,\"value\":null}]"));
        await using var source = new FileRecordSource(input, "json", 1, 1024);
        await using var sink = new PostgreSqlConnector(connectionString);
        try
        {
            var imported = await new DatabaseTransferRunner().RunAsync(
                source,
                sink,
                new DatabaseTransferOptions
                {
                    Destination = new QualifiedName(tableName, "public"),
                    CreateTable = true,
                    TransactionMode = TransactionMode.Batch,
                    UseNativeBulk = true,
                },
                new TransferOptions { BufferBatches = 1, MemoryBudgetBytes = 2048 });
            Assert.Equal(2, imported.Written);

            await using var databaseSource = new DatabaseSourceConnector(
                NpgsqlFactory.Instance,
                connectionString,
                $"SELECT id, value FROM public.\"{tableName}\" ORDER BY id",
                1,
                1024);
            await using var output = new MemoryStream();
            await using var fileSink = new FileRecordSink(output, "csv");
            var exported = await new TransferEngine().RunAsync(
                databaseSource,
                fileSink,
                new TransferOptions { BufferBatches = 1, MemoryBudgetBytes = 2048 });
            await fileSink.CompleteAsync();

            Assert.Equal(2, exported.Written);
            Assert.Equal("id,value\n1,alpha\n2,\\N\n", Encoding.UTF8.GetString(output.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal));
        }
        finally
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP TABLE IF EXISTS public.\"{tableName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
