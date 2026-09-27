using CommandLine;

/// <summary>データベース間でレコードを直接転送する copy コマンドのオプションを保持します。</summary>
[Verb("copy", HelpText = "Copy records between databases.")]
public sealed class CopyOptions : CommonOptions
{
    [Option("source-provider", Required = true, HelpText = "Source provider (SQL, cosmosdb, mongodb, or azure-table-storage).")]
    public string SourceProvider { get; init; } = string.Empty;

    [Option("source-connection", Required = true, HelpText = "Source ADO.NET connection string.")]
    public string SourceConnection { get; init; } = string.Empty;

    [Option("query", HelpText = "Inline SQL, or a document-provider location|query expression; never rewritten.")]
    public string? Query { get; init; }

    [Option("query-file", HelpText = "File containing the query; mutually exclusive with --query.")]
    public string? QueryFile { get; init; }

    [Option("destination-provider", Required = true, HelpText = "Destination provider (SQL, cosmosdb, mongodb, or azure-table-storage).")]
    public string DestinationProvider { get; init; } = string.Empty;

    [Option("destination-connection", Required = true, HelpText = "Destination ADO.NET connection string.")]
    public string DestinationConnection { get; init; } = string.Empty;

    [Option("destination-table", Required = true, HelpText = "Destination table as [catalog.]schema.name.")]
    public string DestinationTable { get; init; } = string.Empty;

    [Option("create-table", HelpText = "Create the destination table from the source schema.")]
    public bool CreateTable { get; init; }

    [Option("map", Separator = ',', HelpText = "Column mappings as source=destination, separated by commas.")]
    public IEnumerable<string> Mappings { get; init; } = [];

    [Option("transaction", Default = "batch", HelpText = "Transaction scope: none, batch, or all.")]
    public string Transaction { get; init; } = "batch";

    [Option("no-native-bulk", HelpText = "Use parameterized inserts instead of the provider native bulk API.")]
    public bool NoNativeBulk { get; init; }

    [Option("checkpoint", HelpText = "Path for an atomic batch checkpoint.")]
    public string? Checkpoint { get; init; }

    [Option("resume", HelpText = "Resume by skipping batches recorded in the checkpoint.")]
    public bool Resume { get; init; }
}
