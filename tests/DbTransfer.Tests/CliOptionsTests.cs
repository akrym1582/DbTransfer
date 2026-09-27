using CommandLine;

namespace DbTransfer.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void Common_options_expose_logging_but_not_placeholder_job_option()
    {
        var options = typeof(CommonOptions).GetProperties()
            .Select(property => property.GetCustomAttributes(typeof(OptionAttribute), true).Cast<OptionAttribute>().SingleOrDefault())
            .OfType<OptionAttribute>()
            .Select(attribute => attribute.LongName)
            .ToArray();

        Assert.Contains("log-file", options);
        Assert.Contains("log-directory", options);
        Assert.Contains("log-retention-days", options);
        Assert.Contains("progress-interval", options);
        Assert.DoesNotContain("job", options);
    }

    [Fact]
    public void Exec_requires_provider_connection_and_sql()
    {
        var names = typeof(ExecOptions).GetProperties()
            .Select(property => property.GetCustomAttributes(typeof(OptionAttribute), true).Cast<OptionAttribute>().SingleOrDefault())
            .OfType<OptionAttribute>()
            .Where(attribute => attribute.Required)
            .Select(attribute => attribute.LongName)
            .ToArray();

        Assert.Contains("provider", names);
        Assert.Contains("connection", names);
        Assert.Contains("sql", names);
    }

    [Theory]
    [InlineData(typeof(CopyOptions))]
    [InlineData(typeof(ExportOptions))]
    [InlineData(typeof(InspectOptions))]
    [InlineData(typeof(ValidateOptions))]
    public void Query_commands_offer_inline_and_file_options(Type optionType)
    {
        var names = optionType.GetProperties()
            .Select(property => property.GetCustomAttributes(typeof(OptionAttribute), true).Cast<OptionAttribute>().SingleOrDefault())
            .OfType<OptionAttribute>()
            .Select(attribute => attribute.LongName)
            .ToArray();

        Assert.Contains("query", names);
        Assert.Contains("query-file", names);
    }

    [Theory]
    [InlineData(typeof(ExportOptions))]
    [InlineData(typeof(ImportOptions))]
    public void Script_commands_offer_file_and_inline_options(Type optionType)
    {
        var names = optionType.GetProperties()
            .Select(property => property.GetCustomAttributes(typeof(OptionAttribute), true).Cast<OptionAttribute>().SingleOrDefault())
            .OfType<OptionAttribute>()
            .Select(attribute => attribute.LongName)
            .ToArray();

        Assert.Contains("script", names);
        Assert.Contains("script-text", names);
    }
}
