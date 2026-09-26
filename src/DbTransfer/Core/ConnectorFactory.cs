using System.Data.Common;
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
        _ => throw new ArgumentException($"Unsupported provider '{provider}'.", nameof(provider)),
    };

    private static string Normalize(string value) => value.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
    {
        "postgres" or "postgresql" => "postgresql",
        "mssql" or "sqlserver" => "sqlserver",
        var other => other,
    };
}
