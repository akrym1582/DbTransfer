using CommandLine;

/// <summary>データベースのレコードをファイルまたは標準出力へ書き出す export コマンドのオプションを保持します。</summary>
[Verb("export", HelpText = "Export database records to a file or stdout.")]
public sealed class ExportOptions : CommonOptions
{
    [Option("provider", Required = true, HelpText = "Source provider (SQL, cosmosdb, mongodb, or azure-table-storage).")]
    public string Provider { get; init; } = string.Empty;

    [Option("connection", Required = true, HelpText = "Source database connection string.")]
    public string Connection { get; init; } = string.Empty;

    [Option("query", Required = true, HelpText = "SQL, or a document-provider location|query expression; never rewritten.")]
    public string Query { get; init; } = string.Empty;

    [Option("output", HelpText = "Output file, or '-' for stdout.", Default = "-")]
    public string Output { get; init; } = "-";

    [Option("format", HelpText = "Output format: csv, json, jsonl, or extended-json.", Default = "jsonl")]
    public string Format { get; init; } = "jsonl";

    [Option("script", HelpText = "Path to an in-process C# record script (.csx).")]
    public string? Script { get; init; }

    [Option("script-argument", Separator = ',', HelpText = "C# script argument in name=value form; available through Arguments.")]
    public IEnumerable<string> ScriptArguments { get; init; } = [];
}
