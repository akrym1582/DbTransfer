using CommandLine;

/// <summary>データベースのスキーマと利用可能なコネクター機能を調べる inspect コマンドのオプションを保持します。</summary>
[Verb("inspect", HelpText = "Inspect schemas and connector capabilities.")]
public sealed class InspectOptions : CommonOptions
{
    [Option("provider", Required = true, HelpText = "Provider (SQL, cosmosdb, mongodb, or azure-table-storage).")]
    public string Provider { get; init; } = string.Empty;

    [Option("connection", Required = true, HelpText = "ADO.NET connection string.")]
    public string Connection { get; init; } = string.Empty;

    [Option("query", HelpText = "Inline query whose result schema will be inspected without rewriting.")]
    public string? Query { get; init; }

    [Option("query-file", HelpText = "File containing the query; mutually exclusive with --query.")]
    public string? QueryFile { get; init; }
}
