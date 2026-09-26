using CommandLine;
using DbTransfer.Core;
using DbTransfer.Formats;

return await Parser.Default.ParseArguments<CopyOptions, ExportOptions, ImportOptions, ExecOptions, InspectOptions, ValidateOptions>(args)
    .MapResult(
        (CopyOptions options) => RunCopyAsync(options),
        (ExportOptions options) => RunExportAsync(options),
        (ImportOptions options) => RunImportAsync(options),
        (ExecOptions _) => NotImplementedAsync("exec"),
        (InspectOptions _) => NotImplementedAsync("inspect"),
        (ValidateOptions _) => NotImplementedAsync("validate"),
        _ => Task.FromResult(2));

static Task<int> NotImplementedAsync(string verb)
{
    Console.Error.WriteLine($"The '{verb}' command is registered but no database connector is installed yet.");
    return Task.FromResult(3);
}

static async Task<int> RunExportAsync(ExportOptions options)
{
    try
    {
        await using var databaseSource = new DatabaseSourceConnector(
            ConnectorFactory.Provider(options.Provider), options.Connection, options.Query, options.BatchSize, options.MaxBatchBytes);
        ISourceConnector source = string.IsNullOrWhiteSpace(options.Script)
            ? databaseSource
            : new ScriptTransformSource(databaseSource, options.Script, options.ScriptArguments, checked((int)Math.Min(options.MaxBatchBytes, int.MaxValue)));
        await using var output = OpenOutput(options.Output);
        await using var sink = new FileRecordSink(output, options.Format);
        var result = await new TransferEngine().RunAsync(source, sink, TransferSettings(options)).ConfigureAwait(false);
        await sink.CompleteAsync().ConfigureAwait(false);
        Console.Error.WriteLine($"Read {result.Read}; wrote {result.Written}; status {result.Status}.");
        return result.Status == WriteStatus.Succeeded ? 0 : 1;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static async Task<int> RunImportAsync(ImportOptions options)
{
    try
    {
        await using var input = OpenInput(options.Input);
        await using var fileSource = new FileRecordSource(input, options.Format, options.BatchSize, options.MaxBatchBytes);
        ISourceConnector source = string.IsNullOrWhiteSpace(options.Script)
            ? fileSource
            : new ScriptTransformSource(fileSource, options.Script, options.ScriptArguments, checked((int)Math.Min(options.MaxBatchBytes, int.MaxValue)));
        await using var sink = ConnectorFactory.Sink(options.DestinationProvider, options.DestinationConnection);
        var result = await new DatabaseTransferRunner().RunAsync(
            source,
            sink,
            new DatabaseTransferOptions
            {
                Destination = ParseName(options.DestinationTable),
                CreateTable = options.CreateTable,
                ColumnMappings = ParseMappings(options.Mappings),
                TransactionMode = Enum.Parse<TransactionMode>(options.Transaction, true),
                UseNativeBulk = !options.NoNativeBulk,
            },
            TransferSettings(options)).ConfigureAwait(false);
        Console.Error.WriteLine($"Read {result.Read}; wrote {result.Written}; status {result.Status}.");
        return result.Status == WriteStatus.Succeeded ? 0 : 1;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static TransferOptions TransferSettings(CommonOptions options) => new()
{
    BufferBatches = options.BufferBatches,
    MemoryBudgetBytes = checked(options.MemoryBudgetMb * 1024L * 1024L),
};

static Stream OpenInput(string path) => path == "-"
    ? new NonClosingStream(Console.OpenStandardInput())
    : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

static Stream OpenOutput(string path) => path == "-"
    ? new NonClosingStream(Console.OpenStandardOutput())
    : new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

static async Task<int> RunCopyAsync(CopyOptions options)
{
    try
    {
        var source = new DatabaseSourceConnector(
            ConnectorFactory.Provider(options.SourceProvider),
            options.SourceConnection,
            options.Query,
            options.BatchSize,
            options.MaxBatchBytes);
        await using (source.ConfigureAwait(false))
        {
            await using var sink = ConnectorFactory.Sink(options.DestinationProvider, options.DestinationConnection);
            var databaseOptions = new DatabaseTransferOptions
            {
                Destination = ParseName(options.DestinationTable),
                CreateTable = options.CreateTable,
                ColumnMappings = ParseMappings(options.Mappings),
                TransactionMode = Enum.Parse<TransactionMode>(options.Transaction, true),
                UseNativeBulk = !options.NoNativeBulk,
                CheckpointFile = options.Checkpoint,
                Resume = options.Resume,
            };
            databaseOptions = databaseOptions with
            {
                PlanFingerprint = TransferPlanFingerprint.Create(
                    options.SourceProvider,
                    options.SourceConnection,
                    options.Query,
                    options.DestinationProvider,
                    options.DestinationConnection,
                    databaseOptions,
                    options.BatchSize,
                    options.MaxBatchBytes),
            };
            var result = await new DatabaseTransferRunner().RunAsync(
                source,
                sink,
                databaseOptions,
                new TransferOptions { BufferBatches = options.BufferBatches, MemoryBudgetBytes = checked(options.MemoryBudgetMb * 1024L * 1024L) });
            Console.Error.WriteLine($"Read {result.Read}; wrote {result.Written}; status {result.Status}.");
            return result.Status == WriteStatus.Succeeded ? 0 : 1;
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static QualifiedName ParseName(string text)
{
    var parts = text.Split('.', StringSplitOptions.TrimEntries);
    return parts.Length switch
    {
        1 => new QualifiedName(parts[0]),
        2 => new QualifiedName(parts[1], parts[0]),
        3 => new QualifiedName(parts[2], parts[1], parts[0]),
        _ => throw new ArgumentException("Destination table must have one to three name components."),
    };
}

static IReadOnlyDictionary<string, string> ParseMappings(IEnumerable<string> values)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var value in values)
    {
        var pair = value.Split('=', 2, StringSplitOptions.TrimEntries);
        if (pair.Length != 2 || pair.Any(string.IsNullOrWhiteSpace) || !result.TryAdd(pair[0], pair[1]))
        {
            throw new ArgumentException($"Invalid or duplicate column mapping '{value}'.");
        }
    }

    return result;
}
