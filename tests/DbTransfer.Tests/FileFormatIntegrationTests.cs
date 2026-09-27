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

    [Theory]
    [InlineData("json")]
    [InlineData("jsonl")]
    public async Task Json_numbers_preserve_fractions_when_the_first_value_is_integral(string format)
    {
        var separator = format == "json" ? "," : "\n";
        var content = format == "json" ? "[{\"v\":1},{\"v\":1.5}]" : $"{{\"v\":1}}{separator}{{\"v\":1.5}}";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await using var source = new FileRecordSource(stream, format, 10, 4096);

        var batch = await source.ReadAsync(CancellationToken.None).SingleAsync();

        Assert.Equal(typeof(decimal), batch.Schema.Columns[0].DataType);
        Assert.Equal(1m, batch.Rows[0][0]);
        Assert.Equal(1.5m, batch.Rows[1][0]);
    }

    [Fact]
    public async Task Extended_json_preserves_offset_free_dates_as_unspecified_DateTime()
    {
        const string timestamp = "2026-09-26T12:34:56.0000000";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{{\"date\":{{\"$date\":\"{timestamp}\"}}}}\n"));
        await using var source = new FileRecordSource(stream, "extended-json", 10, 4096);

        var batch = await source.ReadAsync(CancellationToken.None).SingleAsync();

        var date = Assert.IsType<DateTime>(batch.Rows[0][0]);
        Assert.Equal(DateTimeKind.Unspecified, date.Kind);
        Assert.Equal(DateTime.Parse(timestamp), date);
    }

    [Fact]
    public async Task Csharp_script_transforms_records_in_process()
    {
        var schema = new RecordSchema([new RecordColumn("value", typeof(string))]);
        var inner = new TestSource(schema, [["before"]]);
        var path = await WriteScriptAsync("Record[\"value\"] = ((string)Record[\"value\"]!).ToUpperInvariant() + Arguments[\"suffix\"];");
        var source = new CSharpScriptTransformSource(inner, await File.ReadAllTextAsync(path), new Dictionary<string, string> { ["suffix"] = "!" }, 1024, path);
        await using var enumerator = source.ReadAsync(CancellationToken.None).GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("BEFORE!", enumerator.Current.Rows[0][0]);
        Assert.False(await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task Csharp_script_recalculates_and_bounds_transformed_batches()
    {
        var schema = new RecordSchema([new RecordColumn("value", typeof(string))]);
        var inner = new TestSource(schema, [[new string('a', 50)], [new string('b', 50)]]);
        var path = await WriteScriptAsync("Record[\"value\"] = Record[\"value\"];");
        var source = new CSharpScriptTransformSource(inner, await File.ReadAllTextAsync(path), new Dictionary<string, string>(), 75, path);
        var batches = await source.ReadAsync(CancellationToken.None).ToListAsync();

        Assert.Equal(2, batches.Count);
        Assert.All(batches, batch => Assert.InRange(batch.EstimatedBytes, 50, 75));
    }

    [Fact]
    public async Task Csharp_script_rejects_schema_changes()
    {
        var schema = new RecordSchema([new RecordColumn("value", typeof(string))]);
        var inner = new TestSource(schema, [["value"]]);
        var path = await WriteScriptAsync("Record[\"extra\"] = 1;");
        var source = new CSharpScriptTransformSource(inner, await File.ReadAllTextAsync(path), new Dictionary<string, string>(), 1024, path);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await source.ReadAsync(CancellationToken.None).ToListAsync());

        Assert.Contains("preserve", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> WriteScriptAsync(string code)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dbtransfer-{Guid.NewGuid():N}.csx");
        await File.WriteAllTextAsync(path, code);
        return path;
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
