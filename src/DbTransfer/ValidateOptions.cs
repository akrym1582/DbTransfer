using CommandLine;

/// <summary>データを書き込まずに転送設定と互換性を確認する validate コマンドのオプションを保持します。</summary>
[Verb("validate", HelpText = "Validate database connectivity and a query without transferring rows.")]
public sealed class ValidateOptions : CommonOptions
{
    [Option("provider", Required = true, HelpText = "Provider (SQL, cosmosdb, mongodb, or azure-table-storage).")]
    public string Provider { get; init; } = string.Empty;

    [Option("connection", Required = true, HelpText = "ADO.NET connection string.")]
    public string Connection { get; init; } = string.Empty;

    [Option("query", Required = true, HelpText = "Query to validate and describe without transferring rows.")]
    public string Query { get; init; } = string.Empty;
}
