using System.Text.Json;

namespace DbTransfer.Core;

internal static class DocumentConnector
{
    public static void ValidateBatchLimits(int batchSize, long maxBatchBytes)
    {
        if (batchSize < 1 || maxBatchBytes < 1)
        {
            throw new ArgumentOutOfRangeException(batchSize < 1 ? nameof(batchSize) : nameof(maxBatchBytes));
        }
    }

    public static (string Location, string Query) SplitQuery(string value, string example)
    {
        var parts = value.Split('|', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new ArgumentException($"Document-provider queries must use '{example}'.", nameof(value));
        }

        return (parts[0].Trim(), parts[1].Trim());
    }

    public static (string First, string Second) SplitLocation(string value, string example)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"Location must use '{example}'.", nameof(value));
        }

        return (parts[0], parts[1]);
    }

    public static object? JsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String when value.TryGetDateTimeOffset(out var date) => date,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => value.Clone(),
    };

    public static long Estimate(IEnumerable<object?> values) => values.Sum(value => value switch
    {
        null => 1,
        string text => Math.Max(1, System.Text.Encoding.UTF8.GetByteCount(text)),
        byte[] bytes => bytes.LongLength,
        JsonElement json => System.Text.Encoding.UTF8.GetByteCount(json.GetRawText()),
        _ => 16,
    });

    public static Dictionary<string, object?> Row(RecordSchema schema, IReadOnlyList<string> names, object?[] values) =>
        names.Select((name, index) => new KeyValuePair<string, object?>(name, values[index]))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
}
