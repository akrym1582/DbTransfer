using CommandLine;

/// <summary>データベースのスキーマと利用可能なコネクター機能を調べる inspect コマンドのオプションを保持します。</summary>
[Verb("inspect", HelpText = "Inspect schemas and connector capabilities.")]
public sealed class InspectOptions : CommonOptions;
