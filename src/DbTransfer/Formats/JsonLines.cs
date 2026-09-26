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
        var boundedReader = new BoundedLineReader(reader, maxRecordBytes);
        while (await boundedReader.ReadLineAsync(lineNumber + 1, cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
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

    private sealed class BoundedLineReader(StreamReader reader, int maxRecordBytes)
    {
        private readonly char[] characterBuffer = new char[4096];
        private int bufferedCharacterCount;
        private int bufferedCharacterIndex;
        private int? pendingCharacter;

        public async ValueTask<string?> ReadLineAsync(int lineNumber, CancellationToken cancellationToken)
        {
            var line = new StringBuilder(Math.Min(maxRecordBytes, 4096));
            var byteCount = 0;
            var hasContent = false;

            while (true)
            {
                var value = await ReadCharacterAsync(cancellationToken).ConfigureAwait(false);
                if (value < 0) return hasContent ? line.ToString() : null;

                var character = (char)value;
                if (character is '\r' or '\n')
                {
                    if (character == '\r')
                    {
                        var next = await ReadCharacterAsync(cancellationToken).ConfigureAwait(false);
                        if (next >= 0 && next != '\n') pendingCharacter = next;
                    }

                    return line.ToString();
                }

                hasContent = true;
                var characterBytes = char.IsHighSurrogate(character)
                    ? 0
                    : char.IsLowSurrogate(character) ? 4 : character <= 0x7f ? 1 : character <= 0x7ff ? 2 : 3;
                if (byteCount > maxRecordBytes - characterBytes)
                    throw new InvalidDataException($"JSONL record {lineNumber} exceeds the size limit.");

                byteCount += characterBytes;
                line.Append(character);
            }
        }

        private async ValueTask<int> ReadCharacterAsync(CancellationToken cancellationToken)
        {
            if (pendingCharacter is { } character)
            {
                pendingCharacter = null;
                return character;
            }

            if (bufferedCharacterIndex >= bufferedCharacterCount)
            {
                bufferedCharacterCount = await reader.ReadAsync(characterBuffer, cancellationToken).ConfigureAwait(false);
                bufferedCharacterIndex = 0;
                if (bufferedCharacterCount == 0) return -1;
            }

            return characterBuffer[bufferedCharacterIndex++];
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
