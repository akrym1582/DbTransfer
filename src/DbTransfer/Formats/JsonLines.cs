using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace DbTransfer.Formats;

public static class JsonLines
{
    public static async IAsyncEnumerable<JsonElement> ReadAsync(Stream input, int maxRecordBytes = 4 * 1024 * 1024,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (maxRecordBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxRecordBytes));
        using var reader = new StreamReader(input, new UTF8Encoding(false, true), true, 4096, leaveOpen: true);
        var lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (Encoding.UTF8.GetByteCount(line) > maxRecordBytes)
                throw new InvalidDataException($"JSONL record {lineNumber} exceeds the size limit.");
            JsonDocument document;
            try { document = JsonDocument.Parse(line); }
            catch (JsonException ex) { throw new InvalidDataException($"Invalid JSONL at line {lineNumber}.", ex); }
            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException($"JSONL record {lineNumber} must be an object.");
                yield return document.RootElement.Clone();
            }
        }
    }

    public static async Task WriteAsync(IAsyncEnumerable<JsonElement> records, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(output);
        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            await JsonSerializer.SerializeAsync(output, record, cancellationToken: cancellationToken).ConfigureAwait(false);
            await output.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        }
    }
}
