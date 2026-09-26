using System.Text;
using System.Text.Json;
using DbTransfer.Core;

namespace DbTransfer.Formats;

public sealed class FileRecordSink(Stream output, string format) : ISinkConnector, IAsyncDisposable
{
    private readonly string normalizedFormat = format.ToLowerInvariant();
    private RecordSchema? schema;
    private Utf8JsonWriter? jsonWriter;
    private StreamWriter? csvWriter;
    private bool completed;

    public ConnectorCapabilities Capabilities => ConnectorCapabilities.OrderedWrites;

    public async ValueTask ValidateAsync(RecordSchema value, CancellationToken cancellationToken)
    {
        schema = value;
        if (normalizedFormat == "csv")
        {
            csvWriter = new StreamWriter(output, new UTF8Encoding(false), 4096, true);
            await CsvRecords.WriteRowAsync(csvWriter, value.Columns.Select(c => c.Name), cancellationToken).ConfigureAwait(false);
        }
        else if (normalizedFormat is "json" or "jsonl" or "extended-json")
        {
            if (normalizedFormat == "json")
            {
                jsonWriter = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false });
                jsonWriter.WriteStartArray();
            }
        }
        else
        {
            throw new ArgumentException($"Unsupported format '{format}'.", nameof(format));
        }
    }

    public async ValueTask<WriteResult> WriteAsync(RecordBatch batch, CancellationToken cancellationToken)
    {
        foreach (var row in batch.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (csvWriter is not null)
            {
                await CsvRecords.WriteRowAsync(csvWriter, row, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (normalizedFormat is "jsonl" or "extended-json")
            {
                using var lineWriter = new Utf8JsonWriter(output);
                RecordJson.WriteObject(lineWriter, schema!, row, normalizedFormat == "extended-json");
                await lineWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
                await output.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                RecordJson.WriteObject(jsonWriter!, schema!, row, false);
                await jsonWriter!.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return WriteResult.Success(batch.Count);
    }

    public async ValueTask CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (completed)
        {
            return;
        }

        completed = true;
        if (jsonWriter is not null)
        {
            if (normalizedFormat == "json")
            {
                jsonWriter.WriteEndArray();
            }

            await jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        if (csvWriter is not null)
        {
            await csvWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CompleteAsync().ConfigureAwait(false);
        jsonWriter?.Dispose();
        if (csvWriter is not null)
        {
            await csvWriter.DisposeAsync().ConfigureAwait(false);
        }
    }
}
