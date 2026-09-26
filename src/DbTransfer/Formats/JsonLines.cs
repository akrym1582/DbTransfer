using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace DbTransfer.Formats;

/// <summary>一行を一つの JSON オブジェクトとして扱い、全件を保持せずに JSON Lines を読み書きします。</summary>
public static class JsonLines
{
    /// <summary>UTF-8 の入力を一行ずつ解析し、空白行を除いた JSON オブジェクトを順次返します。</summary>
    /// <param name="input">読み取る JSON Lines データのストリームです。列挙の終了後も開いたままにします。</param>
    /// <param name="maxRecordBytes">一つのレコードに許可する UTF-8 バイト数の上限です。</param>
    /// <param name="cancellationToken">ストリームの読み取りと列挙を中止する要求を受け取るトークンです。</param>
    /// <returns>入力順に複製された JSON オブジェクトを返す非同期シーケンスです。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> が <see langword="null"/> の場合に発生します。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRecordBytes"/> が 1 未満の場合に発生します。</exception>
    /// <exception cref="InvalidDataException">行が有効な JSON オブジェクトでない場合、またはバイト数の上限を超えた場合に発生します。</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> を通じて読み取りの中止が要求された場合に発生します。</exception>
    public static async IAsyncEnumerable<JsonElement> ReadAsync(
        Stream input,
        int maxRecordBytes = 4 * 1024 * 1024,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRecordBytes, 1);

        using var reader = new StreamReader(input, new UTF8Encoding(false, true), true, 4096, leaveOpen: true);
        var lineNumber = 0;
        var boundedReader = new BoundedLineReader(reader, maxRecordBytes);
        while (await boundedReader.ReadLineAsync(lineNumber + 1, cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Invalid JSONL at line {lineNumber}.", ex);
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException($"JSONL record {lineNumber} must be an object.");
                }

                yield return document.RootElement.Clone();
            }
        }
    }

    /// <summary>各 JSON 値を UTF-8 で直列化し、値ごとに改行を付けて出力ストリームへ順次書き込みます。</summary>
    /// <param name="records">出力順に JSON 値を返す非同期シーケンスです。</param>
    /// <param name="output">JSON Lines データを書き込むストリームです。処理の終了後も開いたままにします。</param>
    /// <param name="cancellationToken">列挙とストリームへの書き込みを中止する要求を受け取るトークンです。</param>
    /// <returns>すべての値と改行の書き込みが完了したときに完了するタスクです。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="records"/> または <paramref name="output"/> が <see langword="null"/> の場合に発生します。</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> を通じて書き込みの中止が要求された場合に発生します。</exception>
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
                if (value < 0)
                {
                    return hasContent ? line.ToString() : null;
                }

                var character = (char)value;
                if (character is '\r' or '\n')
                {
                    if (character == '\r')
                    {
                        var next = await ReadCharacterAsync(cancellationToken).ConfigureAwait(false);
                        if (next >= 0 && next != '\n')
                        {
                            pendingCharacter = next;
                        }
                    }

                    return line.ToString();
                }

                hasContent = true;
                var characterBytes = char.IsHighSurrogate(character)
                    ? 0
                    : char.IsLowSurrogate(character) ? 4 : character <= 0x7f ? 1 : character <= 0x7ff ? 2 : 3;
                if (byteCount > maxRecordBytes - characterBytes)
                {
                    throw new InvalidDataException($"JSONL record {lineNumber} exceeds the size limit.");
                }

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
                if (bufferedCharacterCount == 0)
                {
                    return -1;
                }
            }

            return characterBuffer[bufferedCharacterIndex++];
        }
    }
}
