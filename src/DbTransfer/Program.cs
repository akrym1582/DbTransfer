using CommandLine;

return await Parser.Default.ParseArguments<CopyOptions, ExportOptions, ImportOptions, ExecOptions, InspectOptions, ValidateOptions>(args)
    .MapResult(
        (CopyOptions _) => NotImplementedAsync("copy"),
        (ExportOptions _) => NotImplementedAsync("export"),
        (ImportOptions _) => NotImplementedAsync("import"),
        (ExecOptions _) => NotImplementedAsync("exec"),
        (InspectOptions _) => NotImplementedAsync("inspect"),
        (ValidateOptions _) => NotImplementedAsync("validate"),
        _ => Task.FromResult(2));

static Task<int> NotImplementedAsync(string verb)
{
    Console.Error.WriteLine($"The '{verb}' command is registered but no database connector is installed yet.");
    return Task.FromResult(3);
}

public abstract class CommonOptions
{
    [Option("job", HelpText = "Path to a job definition.")] public string? Job { get; init; }
    [Option("batch-size", Default = 1000, HelpText = "Maximum number of records in a batch.")] public int BatchSize { get; init; }
    [Option("max-batch-bytes", Default = 4 * 1024 * 1024, HelpText = "Maximum estimated payload bytes in a batch.")] public long MaxBatchBytes { get; init; }
    [Option("buffer-batches", Default = 2, HelpText = "Maximum number of batches buffered between pipeline stages.")] public int BufferBatches { get; init; }
    [Option("memory-budget-mb", Default = 64, HelpText = "Memory budget in MiB for buffered record batches.")] public int MemoryBudgetMb { get; init; }
}

[Verb("copy", HelpText = "Copy records between databases.")] public sealed class CopyOptions : CommonOptions;
[Verb("export", HelpText = "Export database records to a file or stdout.")] public sealed class ExportOptions : CommonOptions;
[Verb("import", HelpText = "Import records from a file or stdin.")] public sealed class ImportOptions : CommonOptions;
[Verb("exec", HelpText = "Execute provider-specific SQL.")] public sealed class ExecOptions : CommonOptions;
[Verb("inspect", HelpText = "Inspect schemas and connector capabilities.")] public sealed class InspectOptions : CommonOptions;
[Verb("validate", HelpText = "Validate a transfer without writing data.")] public sealed class ValidateOptions : CommonOptions;
