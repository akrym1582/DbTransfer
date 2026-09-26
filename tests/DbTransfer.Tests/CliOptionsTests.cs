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

    [Theory]
    [InlineData(typeof(ExecOptions))]
    [InlineData(typeof(InspectOptions))]
    [InlineData(typeof(ValidateOptions))]
    public void Former_placeholder_commands_require_provider_connection_and_sql(Type optionType)
    {
        var names = optionType.GetProperties()
            .Select(property => property.GetCustomAttributes(typeof(OptionAttribute), true).Cast<OptionAttribute>().SingleOrDefault())
            .OfType<OptionAttribute>()
            .Where(attribute => attribute.Required)
            .Select(attribute => attribute.LongName)
            .ToArray();

        Assert.Contains("provider", names);
        Assert.Contains("connection", names);
        Assert.True(names.Contains("query") || names.Contains("sql"));
    }
}
