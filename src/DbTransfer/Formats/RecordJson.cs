using System.Globalization;
using System.Text.Json;
using DbTransfer.Core;

namespace DbTransfer.Formats;

internal static class RecordJson
{
    public static void WriteObject(Utf8JsonWriter writer, RecordSchema schema, object?[] row, bool extended)
    {
        writer.WriteStartObject();
        for (var i = 0; i < schema.Count; i++)
        {
            writer.WritePropertyName(schema.Columns[i].Name);
            WriteValue(writer, row[i], extended);
        }

        writer.WriteEndObject();
    }

    public static (string[] Names, object?[] Values) ReadObject(JsonElement element, bool extended)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Each JSON record must be an object.");
        }

        var names = new List<string>();
        var values = new List<object?>();
        foreach (var property in element.EnumerateObject())
        {
            names.Add(property.Name);
            values.Add(ReadValue(property.Value, extended));
        }

        if (names.Count == 0)
        {
            throw new InvalidDataException("JSON records must have at least one property.");
        }

        return ([.. names], [.. values]);
    }

    public static object? ConvertValue(JsonElement value, Type targetType, bool extended) =>
        ConvertTo(ReadValue(value, extended), targetType);

    public static object? ConvertTo(object? value, Type targetType)
    {
        if (value is null || targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (targetType == typeof(byte[]) && value is string text)
        {
            return Convert.FromBase64String(text);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private static object? ReadValue(JsonElement value, bool extended)
    {
        if (extended && value.ValueKind == JsonValueKind.Object)
        {
            var properties = value.EnumerateObject().ToArray();
            if (properties.Length == 1)
            {
                return properties[0].Name switch
                {
                    "$numberLong" => long.Parse(properties[0].Value.GetString()!, CultureInfo.InvariantCulture),
                    "$numberDecimal" => decimal.Parse(properties[0].Value.GetString()!, CultureInfo.InvariantCulture),
                    "$date" => DateTimeOffset.Parse(properties[0].Value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    "$uuid" => Guid.Parse(properties[0].Value.GetString()!),
                    "$binary" => Convert.FromBase64String(properties[0].Value.GetString()!),
                    _ => throw new InvalidDataException($"Unsupported Extended JSON value '{properties[0].Name}'."),
                };
            }
        }

        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var integer) => integer,
            JsonValueKind.Number when value.TryGetInt64(out var longInteger) => longInteger,
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            _ => throw new InvalidDataException("Record values must be scalar JSON values."),
        };
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value, bool extended)
    {
        if (!extended)
        {
            JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object));
            return;
        }

        switch (value)
        {
            case long number:
                WriteWrapper(writer, "$numberLong", number.ToString(CultureInfo.InvariantCulture));
                break;
            case decimal number:
                WriteWrapper(writer, "$numberDecimal", number.ToString(CultureInfo.InvariantCulture));
                break;
            case DateTime date:
                WriteWrapper(writer, "$date", date.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset date:
                WriteWrapper(writer, "$date", date.ToString("O", CultureInfo.InvariantCulture));
                break;
            case Guid identifier:
                WriteWrapper(writer, "$uuid", identifier.ToString());
                break;
            case byte[] bytes:
                WriteWrapper(writer, "$binary", Convert.ToBase64String(bytes));
                break;
            default:
                JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object));
                break;
        }
    }

    private static void WriteWrapper(Utf8JsonWriter writer, string name, string value)
    {
        writer.WriteStartObject();
        writer.WriteString(name, value);
        writer.WriteEndObject();
    }
}
