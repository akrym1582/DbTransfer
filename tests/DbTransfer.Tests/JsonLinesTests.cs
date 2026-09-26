using System.Text;
using DbTransfer.Formats;

namespace DbTransfer.Tests;

public sealed class JsonLinesTests
{
    [Fact]
    public async Task Reader_skips_empty_lines_and_preserves_large_integer()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\n{\"id\":12345678901234567890}\n"));
        var records = new List<System.Text.Json.JsonElement>();
        await foreach (var record in JsonLines.ReadAsync(stream)) records.Add(record);
        Assert.Single(records);
        Assert.Equal("12345678901234567890", records[0].GetProperty("id").GetRawText());
    }

    [Fact]
    public async Task Reader_reports_line_for_invalid_json()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\nnot-json\n"));
        var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in JsonLines.ReadAsync(stream)) { }
        });
        Assert.Contains("line 2", exception.Message);
    }

    [Fact]
    public async Task Reader_rejects_oversized_whitespace_line_without_reading_the_whole_line()
    {
        var input = new string(' ', 100_000) + "\n{\"id\":1}\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in JsonLines.ReadAsync(stream, maxRecordBytes: 16)) { }
        });

        Assert.Contains("record 1", exception.Message);
        Assert.True(stream.Position < stream.Length);
    }

    [Fact]
    public async Task Reader_applies_limit_to_utf8_bytes()
    {
        await using var accepted = new MemoryStream(Encoding.UTF8.GetBytes("{\"v\":\"é\"}\n"));
        await using var rejected = new MemoryStream(Encoding.UTF8.GetBytes("{\"v\":\"é\"}\n"));

        await foreach (var _ in JsonLines.ReadAsync(accepted, maxRecordBytes: 10)) { }
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in JsonLines.ReadAsync(rejected, maxRecordBytes: 9)) { }
        });
    }
}
