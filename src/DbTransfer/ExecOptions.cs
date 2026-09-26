using CommandLine;

/// <summary>選択したプロバイダーの SQL を実行する exec コマンドのオプションを保持します。</summary>
[Verb("exec", HelpText = "Execute provider-specific SQL.")]
public sealed class ExecOptions : CommonOptions
{
    [Option("provider", Required = true, HelpText = "Provider: postgresql, sqlserver, mysql, or oracle.")]
    public string Provider { get; init; } = string.Empty;

    [Option("connection", Required = true, HelpText = "ADO.NET connection string.")]
    public string Connection { get; init; } = string.Empty;

    [Option("sql", Required = true, HelpText = "SQL to execute without rewriting.")]
    public string Sql { get; init; } = string.Empty;

    [Option("output", Default = "-", HelpText = "Result output file, or '-' for stdout.")]
    public string Output { get; init; } = "-";

    [Option("format", Default = "jsonl", HelpText = "Result format: csv, json, jsonl, or extended-json.")]
    public string Format { get; init; } = "jsonl";
}
