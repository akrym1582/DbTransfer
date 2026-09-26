using System.Data.Common;
using DbTransfer.Core;
using MySqlConnector;

namespace DbTransfer.Connectors.MySql;

public sealed class MySqlConnectorSink(string connectionString)
    : DatabaseSinkConnectorBase(MySqlConnectorFactory.Instance, connectionString, new MySqlDialect())
{
    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.NativeBulk |
        ConnectorCapabilities.BatchTransactions | ConnectorCapabilities.AllTransaction | ConnectorCapabilities.OrderedWrites;

    protected override string GetSqlType(RecordColumn c) => Type.GetTypeCode(c.DataType) switch
    {
        TypeCode.Boolean => "boolean",
        TypeCode.Byte => "tinyint unsigned",
        TypeCode.Int16 => "smallint",
        TypeCode.Int32 => "int",
        TypeCode.Int64 => "bigint",
        TypeCode.Single => "float",
        TypeCode.Double => "double",
        TypeCode.Decimal => "decimal(65,30)",
        TypeCode.DateTime => "datetime(6)",
        TypeCode.String => "longtext",
        _ when c.DataType == typeof(Guid) => "char(36)",
        _ when c.DataType == typeof(byte[]) => "longblob",
        _ => throw new NotSupportedException($"No MySQL type for {c.DataType}."),
    };

    protected override async ValueTask<long> WriteNativeAsync(RecordBatch batch, DbTransaction? tx, CancellationToken token)
    {
        var bulk = new MySqlBulkCopy((MySqlConnection)Connection, (MySqlTransaction?)tx)
        { DestinationTableName = new MySqlDialect().QuoteName(Options.Destination) };
        for (var i = 0; i < DestinationColumns.Count; i++)
        {
            bulk.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, DestinationColumns[i]));
        }

        using var reader = new RecordBatchDataReader(batch);
        var result = await bulk.WriteToServerAsync(reader, token).ConfigureAwait(false);
        return result.RowsInserted;
    }
}
