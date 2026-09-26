using System.Data.Common;
using DbTransfer.Core;
using Microsoft.Data.SqlClient;

namespace DbTransfer.Connectors.SqlServer;

public sealed class SqlServerConnector(string connectionString)
    : DatabaseSinkConnectorBase(SqlClientFactory.Instance, connectionString, new SqlServerDialect())
{
    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.NativeBulk |
        ConnectorCapabilities.BatchTransactions | ConnectorCapabilities.AllTransaction | ConnectorCapabilities.OrderedWrites;

    protected override string GetSqlType(RecordColumn c) => Type.GetTypeCode(c.DataType) switch
    {
        TypeCode.Boolean => "bit",
        TypeCode.Byte => "tinyint",
        TypeCode.Int16 => "smallint",
        TypeCode.Int32 => "int",
        TypeCode.Int64 => "bigint",
        TypeCode.Single => "real",
        TypeCode.Double => "float",
        TypeCode.Decimal => "decimal(38,18)",
        TypeCode.DateTime => "datetime2",
        TypeCode.String => "nvarchar(max)",
        _ when c.DataType == typeof(Guid) => "uniqueidentifier",
        _ when c.DataType == typeof(byte[]) => "varbinary(max)",
        _ => throw new NotSupportedException($"No SQL Server type for {c.DataType}."),
    };

    protected override async ValueTask<long> WriteNativeAsync(RecordBatch batch, DbTransaction? tx, CancellationToken token)
    {
        using var bulk = new SqlBulkCopy((SqlConnection)Connection, SqlBulkCopyOptions.KeepNulls, (SqlTransaction?)tx)
        { DestinationTableName = new SqlServerDialect().QuoteName(Options.Destination), EnableStreaming = true, BatchSize = batch.Count };
        for (var i = 0; i < DestinationColumns.Count; i++)
        {
            bulk.ColumnMappings.Add(i, DestinationColumns[i]);
        }

        using var reader = new RecordBatchDataReader(batch);
        await bulk.WriteToServerAsync(reader, token).ConfigureAwait(false);
        return batch.Count;
    }
}
