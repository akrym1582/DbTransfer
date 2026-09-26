using System.Runtime.CompilerServices;
using System.Text;

namespace DbTransfer.Formats;

public static class CsvRecords
{
    public static async IAsyncEnumerable<string[]> ReadAsync(
        Stream input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(input, new UTF8Encoding(false, true), true, 4096, true);
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var any = false;
        var buffer = new char[1];
        while (await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) != 0)
        {
            var character = buffer[0];
            any = true;
            if (quoted)
            {
                if (character == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                        field.Append('"');
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            if (character == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (character == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && reader.Peek() == '\n')
                {
                    await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                }

                fields.Add(field.ToString());
                field.Clear();
                yield return [.. fields];
                fields.Clear();
                any = false;
            }
            else
            {
                field.Append(character);
            }
        }

        if (quoted)
        {
            throw new InvalidDataException("CSV input ended inside a quoted field.");
        }

        if (any || field.Length != 0 || fields.Count != 0)
        {
            fields.Add(field.ToString());
            yield return [.. fields];
        }
    }

    public static async ValueTask WriteRowAsync(StreamWriter writer, IEnumerable<object?> values, CancellationToken cancellationToken)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                await writer.WriteAsync(',').ConfigureAwait(false);
            }

            first = false;
            var text = value is null
                ? @"\N"
                : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            if (value is not null && text == @"\N")
            {
                text = @"\\N";
            }

            if (text.IndexOfAny([',', '"', '\r', '\n']) >= 0)
            {
                await writer.WriteAsync('"').ConfigureAwait(false);
                await writer.WriteAsync(text.Replace("\"", "\"\"", StringComparison.Ordinal)).ConfigureAwait(false);
                await writer.WriteAsync('"').ConfigureAwait(false);
            }
            else
            {
                await writer.WriteAsync(text).ConfigureAwait(false);
            }
        }

        await writer.WriteLineAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
