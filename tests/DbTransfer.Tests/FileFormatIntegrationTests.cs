using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DbTransfer.Core;
using DbTransfer.Formats;

namespace DbTransfer.Tests;

public sealed class FileFormatIntegrationTests
{
    public static TheoryData<string> Formats => new() { "csv", "json", "jsonl", "extended-json" };

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task Transfer_pipeline_round_trips_each_file_format(string format)
    {
        var schema = new RecordSchema([
            new RecordColumn("id", typeof(int), false),
            new RecordColumn("text", typeof(string)),
        ]);
        var source = new TestSource(schema, [
            [1, "comma, quote \" and newline\n"],
            [2, null],
        ]);
        await using var stream = new MemoryStream();
        await using (var sink = new FileRecordSink(stream, format))
        {
            var result = await new TransferEngine().RunAsync(source, sink, new TransferOptions
            {
                BufferBatches = 1,
                MemoryBudgetBytes = 4096,
            });
            await sink.CompleteAsync();
            Assert.Equal(2, result.Written);
        }

        stream.Position = 0;
        await using var imported = new FileRecordSource(stream, format, 1, 4096);
        var rows = new List<object?[]>();
        await foreach (var batch in imported.ReadAsync(CancellationToken.None))
        {
            rows.AddRange(batch.Rows);
        }

        Assert.Equal(2, rows.Count);
        Assert.Equal("comma, quote \" and newline\n", rows[0][1]);
        Assert.Null(rows[1][1]);
    }

    [Fact]
    public async Task Extended_json_preserves_database_specific_scalar_types()
    {
        var identifier = Guid.NewGuid();
        var timestamp = DateTimeOffset.Parse("2026-09-26T12:34:56+00:00");
        var schema = new RecordSchema([
            new RecordColumn("long", typeof(long)),
            new RecordColumn("decimal", typeof(decimal)),
            new RecordColumn("date", typeof(DateTimeOffset)),
            new RecordColumn("uuid", typeof(Guid)),
            new RecordColumn("binary", typeof(byte[])),
        ]);
        await using var stream = new MemoryStream();
        await using (var sink = new FileRecordSink(stream, "extended-json"))
        {
            await sink.ValidateAsync(schema, CancellationToken.None);
            await sink.WriteAsync(new RecordBatch(schema, [[long.MaxValue, 1.25m, timestamp, identifier, new byte[] { 1, 2, 3 }]], 64), CancellationToken.None);
            await sink.CompleteAsync();
        }

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("$numberLong", json, StringComparison.Ordinal);
        Assert.Contains("$numberDecimal", json, StringComparison.Ordinal);
        Assert.Contains("$date", json, StringComparison.Ordinal);
        Assert.Contains("$uuid", json, StringComparison.Ordinal);
        Assert.Contains("$binary", json, StringComparison.Ordinal);

        stream.Position = 0;
        await using var source = new FileRecordSource(stream, "extended-json", 10, 4096);
        await using var enumerator = source.ReadAsync(CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        var batch = enumerator.Current;
        Assert.False(await enumerator.MoveNextAsync());
        Assert.Equal(long.MaxValue, batch.Rows[0][0]);
        Assert.Equal(1.25m, batch.Rows[0][1]);
        Assert.Equal(timestamp, batch.Rows[0][2]);
        Assert.Equal(identifier, batch.Rows[0][3]);
        Assert.Equal(new byte[] { 1, 2, 3 }, batch.Rows[0][4]);
    }

    [Fact]
    public async Task Executable_hook_transforms_records_in_the_streaming_pipeline()
    {
        if (!File.Exists("/bin/cat"))
        {
            return;
        }

        var schema = new RecordSchema([new RecordColumn("value", typeof(string))]);
        var inner = new TestSource(schema, [["unchanged"]]);
        var source = new ScriptTransformSource(inner, "/bin/cat", null, 1024);
        await using var enumerator = source.ReadAsync(CancellationToken.None).GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("unchanged", enumerator.Current.Rows[0][0]);
        Assert.False(await enumerator.MoveNextAsync());
    }

    private sealed class TestSource(RecordSchema schema, object?[][] rows) : ISourceConnector
    {
        public ConnectorCapabilities Capabilities => ConnectorCapabilities.OrderedWrites;

        public ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(schema);

        public async IAsyncEnumerable<RecordBatch> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return new RecordBatch(schema, rows, 256);
        }
    }
}
