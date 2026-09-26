using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DbTransfer.Core;

namespace DbTransfer.Formats;

public sealed class FileRecordSource(
    Stream input,
    string format,
    int batchSize,
    long maxBatchBytes) : ISourceConnector, IAsyncDisposable
{
    private IAsyncEnumerator<(string[] Names, object?[] Values)>? enumerator;
    private (string[] Names, object?[] Values)? first;
    private RecordSchema? schema;

    public ConnectorCapabilities Capabilities => ConnectorCapabilities.OrderedWrites;

    public async ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken)
    {
        if (schema is not null)
        {
            return schema;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBatchBytes, 1);
        enumerator = ReadRecords(cancellationToken).GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            throw new InvalidDataException("The input contains no records.");
        }

        first = enumerator.Current;
        schema = new RecordSchema(first.Value.Names.Select((name, index) =>
            new RecordColumn(name, first.Value.Values[index]?.GetType() ?? typeof(string), true)));
        return schema;
    }

    public async IAsyncEnumerable<RecordBatch> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var currentSchema = await GetSchemaAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<object?[]>(batchSize);
        long bytes = 0;
        var pending = first;
        var activeEnumerator = enumerator!;
        first = null;
        while (pending is not null || await activeEnumerator.MoveNextAsync().ConfigureAwait(false))
        {
            var record = pending ?? activeEnumerator.Current;
            pending = null;
            if (!record.Names.SequenceEqual(currentSchema.Columns.Select(c => c.Name), StringComparer.Ordinal))
            {
                throw new InvalidDataException("Every input record must have the same properties in the same order.");
            }

            var row = record.Values.Select((value, index) => RecordJson.ConvertTo(value, currentSchema.Columns[index].DataType)).ToArray();
            var rowBytes = Estimate(row);
            if (rowBytes > maxBatchBytes)
            {
                throw new InvalidDataException("An input record exceeds the configured maximum batch bytes.");
            }

            if (rows.Count != 0 && (rows.Count >= batchSize || bytes + rowBytes > maxBatchBytes))
            {
                yield return new RecordBatch(currentSchema, rows, bytes);
                rows.Clear();
                bytes = 0;
            }

            rows.Add(row);
            bytes += rowBytes;
        }

        if (rows.Count != 0)
        {
            yield return new RecordBatch(currentSchema, rows, bytes);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (enumerator is not null)
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static long Estimate(IEnumerable<object?> values) => values.Sum(value => value switch
    {
        null => 1L,
        string text => Encoding.UTF8.GetByteCount(text),
        byte[] bytes => bytes.LongLength,
        _ => 16L,
    });

    private async IAsyncEnumerable<(string[] Names, object?[] Values)> ReadRecords(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var normalized = format.ToLowerInvariant();
        if (normalized == "csv")
        {
            await using var csv = CsvRecords.ReadAsync(input, cancellationToken).GetAsyncEnumerator(cancellationToken);
            if (!await csv.MoveNextAsync().ConfigureAwait(false))
            {
                yield break;
            }

            var header = csv.Current;
            if (header.Length == 0 || header.Any(string.IsNullOrWhiteSpace) || header.Distinct(StringComparer.Ordinal).Count() != header.Length)
            {
                throw new InvalidDataException("CSV headers must be unique and non-empty.");
            }

            while (await csv.MoveNextAsync().ConfigureAwait(false))
            {
                if (csv.Current.Length != header.Length)
                {
                    throw new InvalidDataException("A CSV record does not match the header width.");
                }

                yield return (header, csv.Current.Select(value => value switch
                {
                    @"\N" => null,
                    @"\\N" => @"\N",
                    _ => (object?)value,
                }).ToArray());
            }

            yield break;
        }

        var extended = normalized == "extended-json";
        if (normalized == "jsonl" || extended)
        {
            await foreach (var element in JsonLines.ReadAsync(input, checked((int)Math.Min(maxBatchBytes, int.MaxValue)), cancellationToken))
            {
                yield return RecordJson.ReadObject(element, extended);
            }

            yield break;
        }

        if (normalized == "json")
        {
            await foreach (var element in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(input, cancellationToken: cancellationToken))
            {
                yield return RecordJson.ReadObject(element, false);
            }

            yield break;
        }

        throw new ArgumentException($"Unsupported format '{format}'.", nameof(format));
    }
}
