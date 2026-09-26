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
}
