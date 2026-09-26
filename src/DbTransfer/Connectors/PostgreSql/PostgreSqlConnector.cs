using System.Data.Common;
using DbTransfer.Core;
using Npgsql;

namespace DbTransfer.Connectors.PostgreSql;

public sealed class PostgreSqlConnector(string connectionString)
    : DatabaseSinkConnectorBase(NpgsqlFactory.Instance, connectionString, new PostgreSqlDialect())
{
    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.NativeBulk |
        ConnectorCapabilities.BatchTransactions | ConnectorCapabilities.AllTransaction | ConnectorCapabilities.OrderedWrites;

    protected override string GetSqlType(RecordColumn c) => Type.GetTypeCode(c.DataType) switch
    {
        TypeCode.Boolean => "boolean",
        TypeCode.Byte or TypeCode.Int16 => "smallint",
        TypeCode.Int32 => "integer",
        TypeCode.Int64 => "bigint",
        TypeCode.Single => "real",
        TypeCode.Double => "double precision",
        TypeCode.Decimal => "numeric",
        TypeCode.DateTime => "timestamp",
        TypeCode.String => "text",
        _ when c.DataType == typeof(Guid) => "uuid",
        _ when c.DataType == typeof(byte[]) => "bytea",
        _ => throw new NotSupportedException($"No PostgreSQL type for {c.DataType}."),
    };

    protected override async ValueTask<long> WriteNativeAsync(RecordBatch batch, DbTransaction? tx, CancellationToken token)
    {
        var dialect = new PostgreSqlDialect();
        var sql = $"COPY {dialect.QuoteName(Options.Destination)} ({string.Join(", ", DestinationColumns.Select(dialect.QuoteIdentifier))}) FROM STDIN (FORMAT BINARY)";
        await using var importer = await ((NpgsqlConnection)Connection).BeginBinaryImportAsync(sql, token).ConfigureAwait(false);
        foreach (var row in batch.Rows)
        {
            await importer.StartRowAsync(token).ConfigureAwait(false);
            foreach (var value in row)
            {
                if (value is null)
                {
                    await importer.WriteNullAsync(token).ConfigureAwait(false);
                }
                else
                {
                    await importer.WriteAsync(value, cancellationToken: token).ConfigureAwait(false);
                }
            }
        }

        return checked((long)await importer.CompleteAsync(token).ConfigureAwait(false));
    }
}
