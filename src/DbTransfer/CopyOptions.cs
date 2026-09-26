using CommandLine;

/// <summary>データベース間でレコードを直接転送する copy コマンドのオプションを保持します。</summary>
[Verb("copy", HelpText = "Copy records between databases.")]
public sealed class CopyOptions : CommonOptions;
