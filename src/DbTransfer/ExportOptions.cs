using CommandLine;

/// <summary>データベースのレコードをファイルまたは標準出力へ書き出す export コマンドのオプションを保持します。</summary>
[Verb("export", HelpText = "Export database records to a file or stdout.")]
public sealed class ExportOptions : CommonOptions
{
    [Option("provider", Required = true, HelpText = "Source provider: postgresql, sqlserver, mysql, or oracle.")]
    public string Provider { get; init; } = string.Empty;

    [Option("connection", Required = true, HelpText = "Source database connection string.")]
    public string Connection { get; init; } = string.Empty;

    [Option("query", Required = true, HelpText = "Source SQL query; it is executed without rewriting.")]
    public string Query { get; init; } = string.Empty;

    [Option("output", HelpText = "Output file, or '-' for stdout.", Default = "-")]
    public string Output { get; init; } = "-";

    [Option("format", HelpText = "Output format: csv, json, jsonl, or extended-json.", Default = "jsonl")]
    public string Format { get; init; } = "jsonl";

    [Option("script", HelpText = "Executable record hook. Receives one JSON object on stdin and returns one on stdout.")]
    public string? Script { get; init; }

    [Option("script-arguments", HelpText = "Arguments passed to the record-hook executable.")]
    public string? ScriptArguments { get; init; }
}
