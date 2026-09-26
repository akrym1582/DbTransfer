using System.Data.Common;
using DbTransfer.Connectors.AzureTableStorage;
using DbTransfer.Connectors.CosmosDb;
using DbTransfer.Connectors.MongoDb;
using DbTransfer.Connectors.MySql;
using DbTransfer.Connectors.Oracle;
using DbTransfer.Connectors.PostgreSql;
using DbTransfer.Connectors.SqlServer;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace DbTransfer.Core;

public static class ConnectorFactory
{
    public static DbProviderFactory Provider(string provider) => Normalize(provider) switch
    {
        "postgresql" => NpgsqlFactory.Instance,
        "sqlserver" => SqlClientFactory.Instance,
        "mysql" => MySqlConnectorFactory.Instance,
        "oracle" => OracleClientFactory.Instance,
        _ => throw new ArgumentException($"Unsupported provider '{provider}'.", nameof(provider)),
    };

    public static IDatabaseSink Sink(string provider, string connectionString) => Normalize(provider) switch
    {
        "postgresql" => new PostgreSqlConnector(connectionString),
        "sqlserver" => new SqlServerConnector(connectionString),
        "mysql" => new MySqlConnectorSink(connectionString),
        "oracle" => new OracleConnector(connectionString),
        "cosmosdb" => new CosmosDbSink(connectionString),
        "mongodb" => new MongoDbSink(connectionString),
        "azuretablestorage" => new AzureTableStorageSink(connectionString),
        _ => throw new ArgumentException($"Unsupported provider '{provider}'.", nameof(provider)),
    };

    public static IDatabaseSource Source(string provider, string connectionString, string query, int batchSize, long maxBatchBytes) => Normalize(provider) switch
    {
        "cosmosdb" => new CosmosDbSource(connectionString, query, batchSize, maxBatchBytes),
        "mongodb" => new MongoDbSource(connectionString, query, batchSize, maxBatchBytes),
        "azuretablestorage" => new AzureTableStorageSource(connectionString, query, batchSize, maxBatchBytes),
        _ => new DatabaseSourceConnector(Provider(provider), connectionString, query, batchSize, maxBatchBytes),
    };

    private static string Normalize(string value) => value.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
    {
        "postgres" or "postgresql" => "postgresql",
        "mssql" or "sqlserver" => "sqlserver",
        "cosmos" or "cosmosdb" => "cosmosdb",
        "mongo" or "mongodb" => "mongodb",
        "azuretable" or "azuretables" or "tablestorage" or "azuretablestorage" => "azuretablestorage",
        var other => other,
    };
}
