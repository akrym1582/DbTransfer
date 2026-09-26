using System.Diagnostics;
using System.Text.Json;
using CommandLine;
using DbTransfer.Core;
using DbTransfer.Formats;
using Serilog;

return await Parser.Default.ParseArguments<CopyOptions, ExportOptions, ImportOptions, ExecOptions, InspectOptions, ValidateOptions>(args)
    .MapResult(
        (CopyOptions options) => RunLoggedAsync(options, logger => RunCopyAsync(options, logger)),
        (ExportOptions options) => RunLoggedAsync(options, logger => RunExportAsync(options, logger)),
        (ImportOptions options) => RunLoggedAsync(options, logger => RunImportAsync(options, logger)),
        (ExecOptions options) => RunLoggedAsync(options, logger => RunExecAsync(options, logger)),
        (InspectOptions options) => RunLoggedAsync(options, logger => RunInspectAsync(options, logger)),
        (ValidateOptions options) => RunLoggedAsync(options, logger => RunValidateAsync(options, logger)),
        _ => Task.FromResult(2));

static async Task<int> RunLoggedAsync(CommonOptions options, Func<ILogger, Task<int>> action)
{
    ILogger? logger = null;
    try
    {
        logger = Logging.Create(options);
        return await action(logger).ConfigureAwait(false);
    }
    catch (Exception exception)
    {
        if (logger is null)
        {
            Console.Error.WriteLine(exception.Message);
        }
        else
        {
            logger.Error(exception, "Command failed: {Error}", exception.Message);
        }

        return 1;
    }
    finally
    {
        (logger as IDisposable)?.Dispose();
    }
}

static async Task<int> RunExportAsync(ExportOptions options, ILogger logger)
{
    var stopwatch = Stopwatch.StartNew();
    await using var databaseSource = NewSource(options.Provider, options.Connection, options.Query, options);
    ISourceConnector source = string.IsNullOrWhiteSpace(options.Script) ? databaseSource
        : new ScriptTransformSource(databaseSource, options.Script, options.ScriptArguments, checked((int)Math.Min(options.MaxBatchBytes, int.MaxValue)));
    await using var output = OpenOutput(options.Output);
    await using var sink = new FileRecordSink(output, options.Format);
    var result = await new TransferEngine().RunAsync(source, sink, TransferSettings(options, logger, stopwatch)).ConfigureAwait(false);
    await sink.CompleteAsync().ConfigureAwait(false);
    return Finish(result, stopwatch, logger);
}

static async Task<int> RunImportAsync(ImportOptions options, ILogger logger)
{
    var stopwatch = Stopwatch.StartNew();
    await using var input = OpenInput(options.Input);
    await using var fileSource = new FileRecordSource(input, options.Format, options.BatchSize, options.MaxBatchBytes);
    ISourceConnector source = string.IsNullOrWhiteSpace(options.Script) ? fileSource
        : new ScriptTransformSource(fileSource, options.Script, options.ScriptArguments, checked((int)Math.Min(options.MaxBatchBytes, int.MaxValue)));
    await using var sink = ConnectorFactory.Sink(options.DestinationProvider, options.DestinationConnection);
    var result = await new DatabaseTransferRunner().RunAsync(
        source,
        sink,
        DatabaseOptions(options.DestinationTable, options.CreateTable, options.Mappings, options.Transaction, options.NoNativeBulk),
        TransferSettings(options, logger, stopwatch)).ConfigureAwait(false);
    return Finish(result, stopwatch, logger);
}

static async Task<int> RunCopyAsync(CopyOptions options, ILogger logger)
{
    var stopwatch = Stopwatch.StartNew();
    await using var source = NewSource(options.SourceProvider, options.SourceConnection, options.Query, options);
    await using var sink = ConnectorFactory.Sink(options.DestinationProvider, options.DestinationConnection);
    var databaseOptions = DatabaseOptions(
        options.DestinationTable,
        options.CreateTable,
        options.Mappings,
        options.Transaction,
        options.NoNativeBulk) with
    { CheckpointFile = options.Checkpoint, Resume = options.Resume };
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
        TransferSettings(options, logger, stopwatch)).ConfigureAwait(false);
    return Finish(result, stopwatch, logger);
}

static async Task<int> RunExecAsync(ExecOptions options, ILogger logger)
{
    var stopwatch = Stopwatch.StartNew();
    await using var source = NewSource(options.Provider, options.Connection, options.Sql, options);
    await using var output = OpenOutput(options.Output);
    await using var sink = new FileRecordSink(output, options.Format);
    var result = await new TransferEngine().RunAsync(source, sink, TransferSettings(options, logger, stopwatch)).ConfigureAwait(false);
    await sink.CompleteAsync().ConfigureAwait(false);
    return Finish(result, stopwatch, logger);
}

static async Task<int> RunInspectAsync(InspectOptions options, ILogger logger)
{
    var stopwatch = Stopwatch.StartNew();
    await using var source = NewSource(options.Provider, options.Connection, options.Query, options);
    var schema = await source.GetSchemaAsync(default).ConfigureAwait(false);
    var value = new
    {
        Provider = options.Provider,
        Capabilities = source.Capabilities.ToString(),
        Columns = schema.Columns.Select(column => new { column.Name, Type = column.DataType.FullName, column.IsNullable }),
    };
    await JsonSerializer.SerializeAsync(Console.OpenStandardOutput(), value, cancellationToken: default).ConfigureAwait(false);
    Console.Out.WriteLine();
    logger.Information("Inspected {ColumnCount} columns in {Elapsed}.", schema.Columns.Count, stopwatch.Elapsed);
    return 0;
}

static async Task<int> RunValidateAsync(ValidateOptions options, ILogger logger)
{
    var stopwatch = Stopwatch.StartNew();
    await using var source = NewSource(options.Provider, options.Connection, options.Query, options);
    var schema = await source.GetSchemaAsync(default).ConfigureAwait(false);
    logger.Information("Validation succeeded: {ColumnCount} columns, elapsed {Elapsed}.", schema.Columns.Count, stopwatch.Elapsed);
    return 0;
}

static DatabaseSourceConnector NewSource(string provider, string connection, string query, CommonOptions options) =>
    new(ConnectorFactory.Provider(provider), connection, query, options.BatchSize, options.MaxBatchBytes);

static TransferOptions TransferSettings(CommonOptions options, ILogger logger, Stopwatch stopwatch)
{
    if (options.ProgressInterval < 0)
    {
        throw new ArgumentOutOfRangeException(nameof(options.ProgressInterval), "Progress interval cannot be negative.");
    }

    long next = options.ProgressInterval;
    return new TransferOptions
    {
        BufferBatches = options.BufferBatches,
        MemoryBudgetBytes = checked(options.MemoryBudgetMb * 1024L * 1024L),
        Progress = options.ProgressInterval == 0 ? null : (read, written) =>
        {
            if (written < next)
            {
                return;
            }

            logger.Information("Progress: read {Read} records; wrote {Written} records; elapsed {Elapsed}.", read, written, stopwatch.Elapsed);
            do
            {
                next += options.ProgressInterval;
            }
            while (next <= written);
        },
    };
}

static int Finish(TransferResult result, Stopwatch stopwatch, ILogger logger)
{
    logger.Information(
        "Completed: read {Read} records; wrote {Written} records; status {Status}; elapsed {Elapsed}.",
        result.Read,
        result.Written,
        result.Status,
        stopwatch.Elapsed);
    return result.Status == WriteStatus.Succeeded ? 0 : 1;
}

static DatabaseTransferOptions DatabaseOptions(string table, bool create, IEnumerable<string> mappings, string transaction, bool noNativeBulk) => new()
{
    Destination = ParseName(table),
    CreateTable = create,
    ColumnMappings = ParseMappings(mappings),
    TransactionMode = Enum.Parse<TransactionMode>(transaction, true),
    UseNativeBulk = !noNativeBulk,
};

static Stream OpenInput(string path) => path == "-" ? new NonClosingStream(Console.OpenStandardInput())
    : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
static Stream OpenOutput(string path) => path == "-" ? new NonClosingStream(Console.OpenStandardOutput())
    : new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

static QualifiedName ParseName(string text)
{
    var parts = text.Split('.', StringSplitOptions.TrimEntries);
    return parts.Length switch
    {
        1 => new(parts[0]),
        2 => new(parts[1], parts[0]),
        3 => new(parts[2], parts[1], parts[0]),
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
