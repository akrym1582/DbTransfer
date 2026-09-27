using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace DbTransfer.Core;

/// <summary>Runs an in-process C# script once for each record while preserving its schema.</summary>
public sealed class CSharpScriptTransformSource(
    ISourceConnector inner,
    string scriptText,
    IReadOnlyDictionary<string, string> arguments,
    int maxRecordBytes,
    string scriptName = "inline script") : ISourceConnector
{
    public ConnectorCapabilities Capabilities => inner.Capabilities;

    public ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken) => inner.GetSchemaAsync(cancellationToken);

    public async IAsyncEnumerable<RecordBatch> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptText);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRecordBytes, 1);

        var script = CSharpScript.Create(
            scriptText,
            ScriptOptions.Default
                .AddReferences(typeof(ScriptGlobals).Assembly)
                .AddImports("System", "System.Collections.Generic", "System.Linq", "System.Threading", "System.Threading.Tasks"),
            typeof(ScriptGlobals));
        var diagnostics = script.Compile();
        if (diagnostics.Any(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
        {
            throw new InvalidDataException($"C# script '{scriptName}' did not compile:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics)}");
        }

        var runner = script.CreateDelegate();
        var schema = await inner.GetSchemaAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var batch in inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var transformed = new List<object?[]>(batch.Count);
            long transformedBytes = 0;
            foreach (var row in batch.Rows)
            {
                var record = schema.Columns.Select((column, index) => (column.Name, row[index]))
                    .ToDictionary(pair => pair.Name, pair => pair.Item2, StringComparer.Ordinal);
                await runner(new ScriptGlobals(record, arguments, cancellationToken), cancellationToken).ConfigureAwait(false);
                ValidateKeys(schema, record);

                var result = schema.Columns.Select(column => ConvertValue(record[column.Name], column.DataType)).ToArray();
                var bytes = JsonSerializer.SerializeToUtf8Bytes(record).LongLength;
                if (bytes > maxRecordBytes)
                {
                    throw new InvalidDataException("C# script output exceeds the configured record-size limit.");
                }

                if (transformed.Count != 0 && transformedBytes + bytes > maxRecordBytes)
                {
                    yield return new RecordBatch(schema, transformed, transformedBytes);
                    transformed.Clear();
                    transformedBytes = 0;
                }

                transformed.Add(result);
                transformedBytes += bytes;
            }

            if (transformed.Count != 0)
            {
                yield return new RecordBatch(schema, transformed, transformedBytes);
            }
        }
    }

    private static void ValidateKeys(RecordSchema schema, IDictionary<string, object?> record)
    {
        if (record.Count != schema.Count || schema.Columns.Any(column => !record.ContainsKey(column.Name)))
        {
            throw new InvalidDataException("The C# script must preserve all record property names and must not add properties.");
        }
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null || targetType.IsInstanceOfType(value))
        {
            return value;
        }

        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try
        {
            return type.IsEnum ? Enum.Parse(type, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!, true)
                : Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidDataException($"C# script value '{value}' cannot be converted to {targetType.FullName}.", exception);
        }
    }
}
