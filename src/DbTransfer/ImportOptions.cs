using CommandLine;

/// <summary>ファイルまたは標準入力のレコードをデータベースへ書き込む import コマンドのオプションを保持します。</summary>
[Verb("import", HelpText = "Import records from a file or stdin.")]
public sealed class ImportOptions : CommonOptions
{
    [Option("input", HelpText = "Input file, or '-' for stdin.", Default = "-")]
    public string Input { get; init; } = "-";

    [Option("format", HelpText = "Input format: csv, json, jsonl, or extended-json.", Default = "jsonl")]
    public string Format { get; init; } = "jsonl";

    [Option("destination-provider", Required = true, HelpText = "Destination provider: postgresql, sqlserver, mysql, or oracle.")]
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

    [Option("script", HelpText = "Executable record hook. Receives one JSON object on stdin and returns one on stdout.")]
    public string? Script { get; init; }

    [Option("script-arguments", HelpText = "Arguments passed to the record-hook executable.")]
    public string? ScriptArguments { get; init; }
}
