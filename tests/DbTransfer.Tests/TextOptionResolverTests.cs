namespace DbTransfer.Tests;

public sealed class TextOptionResolverTests
{
    [Fact]
    public async Task Required_text_can_be_inline_or_read_from_a_file()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "select 2");
        try
        {
            Assert.Equal("select 1", await TextOptionResolver.ResolveRequiredAsync("select 1", null, "query"));
            Assert.Equal("select 2", await TextOptionResolver.ResolveRequiredAsync(null, path, "query"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("select 1", "query.sql")]
    public async Task Required_text_rejects_missing_or_conflicting_sources(string? inlineText, string? path)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            TextOptionResolver.ResolveRequiredAsync(inlineText, path, "query"));

        Assert.Contains("exactly one", error.Message);
    }

    [Fact]
    public async Task Optional_text_supports_inline_file_absent_and_rejects_conflicts()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "Record[\"id\"] = 2;");
        try
        {
            Assert.Null(await TextOptionResolver.ResolveOptionalAsync(null, null, "script-text", "script"));
            Assert.Equal("Record[\"id\"] = 1;", await TextOptionResolver.ResolveOptionalAsync("Record[\"id\"] = 1;", null, "script-text", "script"));
            Assert.Equal("Record[\"id\"] = 2;", await TextOptionResolver.ResolveOptionalAsync(null, path, "script-text", "script"));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                TextOptionResolver.ResolveOptionalAsync("code", path, "script-text", "script"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
