using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DbTransfer.Formats;

namespace DbTransfer.Core;

/// <summary>Applies a schema-preserving executable hook to each record without interpreting shell syntax.</summary>
public sealed class ScriptTransformSource(
    ISourceConnector inner,
    string executable,
    string? arguments,
    int maxOutputBytes) : ISourceConnector
{
    public ConnectorCapabilities Capabilities => inner.Capabilities;

    public ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken) => inner.GetSchemaAsync(cancellationToken);

    public async IAsyncEnumerable<RecordBatch> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var schema = await inner.GetSchemaAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var batch in inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var transformed = new List<object?[]>(batch.Count);
            foreach (var row in batch.Rows)
            {
                transformed.Add(await TransformAsync(schema, row, cancellationToken).ConfigureAwait(false));
            }

            yield return new RecordBatch(schema, transformed, batch.EstimatedBytes);
        }
    }

    private async Task<object?[]> TransformAsync(RecordSchema schema, object?[] row, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            Arguments = arguments ?? string.Empty,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start script '{executable}'.");
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await using (var writer = new Utf8JsonWriter(process.StandardInput.BaseStream))
        {
            RecordJson.WriteObject(writer, schema, row, false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        process.StandardInput.Close();

        using var document = await JsonDocument.ParseAsync(
            new BoundedReadStream(process.StandardOutput.BaseStream, maxOutputBytes), cancellationToken: cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Script '{executable}' exited with code {process.ExitCode}: {error.Trim()}");
        }

        var record = RecordJson.ReadObject(document.RootElement, false);
        if (!record.Names.SequenceEqual(schema.Columns.Select(c => c.Name), StringComparer.Ordinal))
        {
            throw new InvalidDataException("The script must return the same properties in the same order.");
        }

        return record.Values.Select((value, index) => RecordJson.ConvertTo(value, schema.Columns[index].DataType)).ToArray();
    }

    private sealed class BoundedReadStream(Stream inner, int limit) : Stream
    {
        private int read;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await inner.ReadAsync(buffer[..Math.Min(buffer.Length, limit - read + 1)], cancellationToken).ConfigureAwait(false);
            read += count;
            if (read > limit)
            {
                throw new InvalidDataException("Script output exceeds the configured record-size limit.");
            }

            return count;
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
