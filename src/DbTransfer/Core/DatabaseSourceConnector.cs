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
                row[i] = await ReadBoundedValueAsync(reader, i, maxBatchBytes - bytes, cancellationToken).ConfigureAwait(false);
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

    private static async ValueTask<object?> ReadBoundedValueAsync(
        DbDataReader currentReader,
        int ordinal,
        long remainingBytes,
        CancellationToken cancellationToken)
    {
        if (await currentReader.IsDBNullAsync(ordinal, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (remainingBytes < 1)
        {
            throw new InvalidOperationException("A source row exceeds the configured maximum batch bytes.");
        }

        var fieldType = currentReader.GetFieldType(ordinal);
        if (fieldType == typeof(byte[]))
        {
            var length = currentReader.GetBytes(ordinal, 0, null, 0, 0);
            EnsureLengthFits(length, remainingBytes);
            var value = new byte[checked((int)length)];
            var read = currentReader.GetBytes(ordinal, 0, value, 0, value.Length);
            if (read != length)
            {
                throw new InvalidOperationException("The provider did not return the complete binary value.");
            }

            return value;
        }

        if (fieldType == typeof(string))
        {
            // With CommandBehavior.SequentialAccess a column can only be read once and only
            // forward, so the length cannot be probed via GetChars before reading the value:
            // doing so consumes the column and leaves GetString unable to re-read it from the
            // start. Instead, stream the characters in bounded chunks, failing fast once the
            // accumulated UTF-8 byte size would exceed the remaining budget.
            var buffer = new char[4096];
            var characters = new System.Text.StringBuilder();
            long bytesSoFar = 0;
            long charsRead;
            long dataOffset = 0;
            while ((charsRead = currentReader.GetChars(ordinal, dataOffset, buffer, 0, buffer.Length)) > 0)
            {
                var chunkLength = checked((int)charsRead);
                bytesSoFar += System.Text.Encoding.UTF8.GetByteCount(buffer, 0, chunkLength);
                EnsureLengthFits(bytesSoFar, remainingBytes);
                characters.Append(buffer, 0, chunkLength);
                dataOffset += charsRead;
            }

            return characters.ToString();
        }

        return currentReader.GetValue(ordinal);
    }

    private static void EnsureLengthFits(long length, long remainingBytes)
    {
        if (length > remainingBytes || length > int.MaxValue)
        {
            throw new InvalidOperationException("A source value exceeds the configured maximum batch bytes.");
        }
    }
}
