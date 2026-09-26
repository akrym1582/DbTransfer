using System.Data.Common;
using System.Runtime.CompilerServices;

namespace DbTransfer.Core;

/// <summary>Streams an arbitrary, unmodified query through a provider DbDataReader.</summary>
public sealed class DatabaseSourceConnector(
    DbProviderFactory factory,
    string connectionString,
    string query,
    int batchSize,
    long maxBatchBytes) : ISourceConnector, IAsyncDisposable
{
    private DbConnection? connection;
    private DbCommand? command;
    private DbDataReader? reader;
    private RecordSchema? schema;

    public ConnectorCapabilities Capabilities => ConnectorCapabilities.OrderedWrites;

    public async ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken)
    {
        if (schema is not null)
        {
            return schema;
        }

        if (batchSize < 1 || maxBatchBytes < 1)
        {
            throw new ArgumentOutOfRangeException(batchSize < 1 ? nameof(batchSize) : nameof(maxBatchBytes));
        }

        connection = factory.CreateConnection() ?? throw new InvalidOperationException("Provider did not create a connection.");
        connection.ConnectionString = connectionString;
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        command = connection.CreateCommand();
        command.CommandText = query; // User SQL is deliberately not rewritten.
        reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
        schema = new RecordSchema(Enumerable.Range(0, reader.FieldCount)
            .Select(i => new RecordColumn(reader.GetName(i), reader.GetFieldType(i), true)));
        return schema;
    }

    public async IAsyncEnumerable<RecordBatch> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var currentSchema = await GetSchemaAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<object?[]>(batchSize);
        long bytes = 0;
        while (await reader!.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = await reader.IsDBNullAsync(i, cancellationToken).ConfigureAwait(false) ? null : reader.GetValue(i);
                bytes += Estimate(row[i]);
            }

            rows.Add(row);
            if (rows.Count >= batchSize || bytes >= maxBatchBytes)
            {
                yield return new RecordBatch(currentSchema, rows, bytes);
                rows.Clear();
                bytes = 0;
            }
        }

        if (rows.Count > 0)
        {
            yield return new RecordBatch(currentSchema, rows, bytes);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (reader is not null)
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }

        if (command is not null)
        {
            await command.DisposeAsync().ConfigureAwait(false);
        }

        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static long Estimate(object? value) => value switch
    {
        null => 1,
        string text => Math.Max(1, System.Text.Encoding.UTF8.GetByteCount(text)),
        byte[] bytes => bytes.LongLength,
        _ => 16,
    };
}
