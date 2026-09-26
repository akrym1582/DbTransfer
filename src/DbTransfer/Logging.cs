using Serilog;

internal static class Logging
{
    public static ILogger Create(CommonOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.LogRetentionDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.LogRetentionDays), "Log retention days must be at least one.");
        }

        if (!string.IsNullOrWhiteSpace(options.LogFile) && !string.IsNullOrWhiteSpace(options.LogDirectory))
        {
            throw new ArgumentException("Specify either --log-file or --log-directory, not both.");
        }

        var configuration = new LoggerConfiguration().MinimumLevel.Information()
            .WriteTo.Console(
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        var path = ResolvePath(options, DateTimeOffset.Now, Environment.ProcessId);
        if (path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            DeleteExpiredFiles(options);
            configuration.WriteTo.File(
                path,
                rollingInterval: RollingInterval.Day,
                retainedFileTimeLimit: TimeSpan.FromDays(options.LogRetentionDays),
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        return configuration.CreateLogger();
    }

    internal static string? ResolvePath(CommonOptions options, DateTimeOffset now, int processId)
    {
        var value = !string.IsNullOrWhiteSpace(options.LogDirectory)
            ? Path.Combine(options.LogDirectory, "dbtransfer-.log")
            : options.LogFile;
        return value?
            .Replace("{Date}", now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{UtcDate}", now.UtcDateTime.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{ProcessId}", processId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteExpiredFiles(CommonOptions options)
    {
        var template = !string.IsNullOrWhiteSpace(options.LogDirectory)
            ? Path.Combine(options.LogDirectory, "dbtransfer-*.log")
            : options.LogFile!
                .Replace("{Date}", "*", StringComparison.OrdinalIgnoreCase)
                .Replace("{UtcDate}", "*", StringComparison.OrdinalIgnoreCase)
                .Replace("{ProcessId}", "*", StringComparison.OrdinalIgnoreCase);
        var fullTemplate = Path.GetFullPath(template);
        var directory = Path.GetDirectoryName(fullTemplate)!;
        var pattern = Path.GetFileName(fullTemplate);
        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            pattern = $"{Path.GetFileNameWithoutExtension(pattern)}*{Path.GetExtension(pattern)}";
        }

        var cutoff = DateTime.UtcNow - TimeSpan.FromDays(options.LogRetentionDays);
        foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
        {
            if (File.GetLastWriteTimeUtc(file) < cutoff)
            {
                File.Delete(file);
            }
        }
    }
}
