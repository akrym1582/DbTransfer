using System.Data.Common;

namespace DbTransfer.Core;

public abstract class DatabaseSinkConnectorBase(
    DbProviderFactory factory,
    string connectionString,
    ISqlDialect dialect) : IDatabaseSink
{
    private DbConnection? connection;
    private DbTransaction? transaction;
    private RecordSchema? schema;
    private DatabaseTransferOptions? options;
    private string[]? destinationColumns;

    public abstract ConnectorCapabilities Capabilities { get; }

    protected DbConnection Connection => connection ?? throw new InvalidOperationException("The sink is not initialized.");

    protected DbTransaction? Transaction => transaction;

    protected RecordSchema Schema => schema ?? throw new InvalidOperationException("The sink is not initialized.");

    protected DatabaseTransferOptions Options => options ?? throw new InvalidOperationException("The sink is not initialized.");

    protected IReadOnlyList<string> DestinationColumns => destinationColumns ?? throw new InvalidOperationException("The sink is not initialized.");

    public ValueTask ValidateAsync(RecordSchema sourceSchema, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceSchema);
        if (options is null)
        {
            throw new InvalidOperationException("InitializeAsync must be called before validation.");
        }

        if (Options.UseNativeBulk && !Capabilities.HasFlag(ConnectorCapabilities.NativeBulk))
        {
            throw new NotSupportedException("This provider does not support native bulk writes.");
        }

        if (Options.TransactionMode == TransactionMode.All && !Capabilities.HasFlag(ConnectorCapabilities.AllTransaction))
        {
            throw new NotSupportedException("This provider does not support whole-transfer transactions.");
        }

        if (Options.TransactionMode == TransactionMode.Batch && !Capabilities.HasFlag(ConnectorCapabilities.BatchTransactions))
        {
            throw new NotSupportedException("This provider does not support batch transactions.");
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask InitializeAsync(RecordSchema sourceSchema, DatabaseTransferOptions transferOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceSchema);
        ArgumentNullException.ThrowIfNull(transferOptions);
        schema = sourceSchema;
        options = transferOptions;
        destinationColumns = sourceSchema.Columns.Select(c =>
            transferOptions.ColumnMappings.TryGetValue(c.Name, out var mapped) ? mapped : c.Name).ToArray();
        if (destinationColumns.Any(string.IsNullOrWhiteSpace) || destinationColumns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != destinationColumns.Length)
        {
            throw new ArgumentException("Column mappings must produce unique, non-empty destination columns.");
        }

        connection = factory.CreateConnection() ?? throw new InvalidOperationException("Provider did not create a connection.");
        connection.ConnectionString = connectionString;
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (transferOptions.CreateTable)
        {
            await using var create = connection.CreateCommand();
            create.CommandText = BuildCreateTableSql();
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (transferOptions.TransactionMode == TransactionMode.All)
        {
            transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<WriteResult> WriteAsync(RecordBatch batch, CancellationToken cancellationToken)
    {
        DbTransaction? batchTransaction = null;
        if (Options.TransactionMode == TransactionMode.Batch)
        {
            batchTransaction = await Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var count = Options.UseNativeBulk
                ? await WriteNativeAsync(batch, batchTransaction ?? transaction, cancellationToken).ConfigureAwait(false)
                : await WriteParameterizedAsync(batch, batchTransaction ?? transaction, cancellationToken).ConfigureAwait(false);
            if (batchTransaction is not null)
            {
                await batchTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return WriteResult.Success(count);
        }
        catch
        {
            if (batchTransaction is not null)
            {
                await batchTransaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (batchTransaction is not null)
            {
                await batchTransaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async ValueTask CompleteAsync(CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await transaction.DisposeAsync().ConfigureAwait(false);
            transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    protected abstract string GetSqlType(RecordColumn column);

    protected abstract ValueTask<long> WriteNativeAsync(RecordBatch batch, DbTransaction? currentTransaction, CancellationToken cancellationToken);

    private string BuildCreateTableSql() => $"CREATE TABLE {dialect.QuoteName(Options.Destination)} (" +
        string.Join(", ", Schema.Columns.Select((column, i) =>
            $"{dialect.QuoteIdentifier(DestinationColumns[i])} {GetSqlType(column)}{(column.IsNullable ? string.Empty : " NOT NULL")}")) + ")";

    private async ValueTask<long> WriteParameterizedAsync(RecordBatch batch, DbTransaction? currentTransaction, CancellationToken cancellationToken)
    {
        await using var command = Connection.CreateCommand();
        command.Transaction = currentTransaction;
        command.CommandText = $"INSERT INTO {dialect.QuoteName(Options.Destination)} (" +
            string.Join(", ", DestinationColumns.Select(dialect.QuoteIdentifier)) + ") VALUES (" +
            string.Join(", ", Enumerable.Range(0, Schema.Count).Select(i => $"@p{i}")) + ")";
        for (var i = 0; i < Schema.Count; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"p{i}";
            command.Parameters.Add(parameter);
        }

        foreach (var row in batch.Rows)
        {
            for (var i = 0; i < row.Length; i++)
            {
                command.Parameters[i].Value = row[i] ?? DBNull.Value;
            }

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return batch.Count;
    }
}
