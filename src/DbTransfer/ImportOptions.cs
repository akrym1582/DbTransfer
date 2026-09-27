using CommandLine;

/// <summary>ファイルまたは標準入力のレコードをデータベースへ書き込む import コマンドのオプションを保持します。</summary>
[Verb("import", HelpText = "Import records from a file or stdin.")]
public sealed class ImportOptions : CommonOptions
{
    [Option("input", HelpText = "Input file, or '-' for stdin.", Default = "-")]
    public string Input { get; init; } = "-";

    [Option("format", HelpText = "Input format: csv, json, jsonl, or extended-json.", Default = "jsonl")]
    public string Format { get; init; } = "jsonl";

    [Option("destination-provider", Required = true, HelpText = "Destination provider (SQL, cosmosdb, mongodb, or azure-table-storage).")]
    public string DestinationProvider { get; init; } = string.Empty;

    [Option("destination-connection", Required = true, HelpText = "Destination database connection string.")]
    public string DestinationConnection { get; init; } = string.Empty;

    [Option("destination-table", Required = true, HelpText = "One-, two-, or three-part destination table name.")]
    public string DestinationTable { get; init; } = string.Empty;

    [Option("create-table", HelpText = "Create the destination table before importing.")]
    public bool CreateTable { get; init; }

    [Option("map", Separator = ',', HelpText = "Column mapping in source=destination form.")]
    public IEnumerable<string> Mappings { get; init; } = [];

    [Option("transaction", Default = "batch", HelpText = "Transaction scope: none, batch, or all.")]
    public string Transaction { get; init; } = "batch";

    [Option("no-native-bulk", HelpText = "Use parameterized inserts instead of native bulk loading.")]
    public bool NoNativeBulk { get; init; }

    [Option("script", HelpText = "Path to an in-process C# record script (.csx).")]
    public string? Script { get; init; }

    [Option("script-text", HelpText = "Inline in-process C# record script; mutually exclusive with --script.")]
    public string? ScriptText { get; init; }

    [Option("script-argument", Separator = ',', HelpText = "C# script argument in name=value form; available through Arguments.")]
    public IEnumerable<string> ScriptArguments { get; init; } = [];
}
