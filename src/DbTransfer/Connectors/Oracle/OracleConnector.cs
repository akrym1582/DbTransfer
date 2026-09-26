using System.Data.Common;
using DbTransfer.Core;
using Oracle.ManagedDataAccess.Client;

namespace DbTransfer.Connectors.Oracle;

public sealed class OracleConnector(string connectionString)
    : DatabaseSinkConnectorBase(OracleClientFactory.Instance, connectionString, new OracleDialect())
{
    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.NativeBulk |
        ConnectorCapabilities.BatchTransactions | ConnectorCapabilities.AllTransaction | ConnectorCapabilities.OrderedWrites;

    protected override string GetSqlType(RecordColumn c) => Type.GetTypeCode(c.DataType) switch
    {
        TypeCode.Boolean => "number(1)",
        TypeCode.Byte => "number(3)",
        TypeCode.Int16 => "number(5)",
        TypeCode.Int32 => "number(10)",
        TypeCode.Int64 => "number(19)",
        TypeCode.Single => "binary_float",
        TypeCode.Double => "binary_double",
        TypeCode.Decimal => "number",
        TypeCode.DateTime => "timestamp",
        TypeCode.String => "clob",
        _ when c.DataType == typeof(Guid) => "raw(16)",
        _ when c.DataType == typeof(byte[]) => "blob",
        _ => throw new NotSupportedException($"No Oracle type for {c.DataType}."),
    };

    protected override async ValueTask<long> WriteNativeAsync(RecordBatch batch, DbTransaction? tx, CancellationToken token)
    {
        var dialect = new OracleDialect();
        await using var command = Connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = $"INSERT INTO {dialect.QuoteName(Options.Destination)} ({string.Join(", ", DestinationColumns.Select(dialect.QuoteIdentifier))}) VALUES ({string.Join(", ", Enumerable.Range(0, Schema.Count).Select(i => $":p{i}"))})";
        ((OracleCommand)command).ArrayBindCount = batch.Count;
        for (var column = 0; column < Schema.Count; column++)
        {
            var values = new object[batch.Count];
            for (var row = 0; row < batch.Count; row++)
            {
                values[row] = batch.Rows[row][column] ?? DBNull.Value;
            }

            command.Parameters.Add(new OracleParameter($"p{column}", values));
        }

        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        return batch.Count;
    }
}
