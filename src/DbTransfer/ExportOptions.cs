using CommandLine;

/// <summary>データベースのレコードをファイルまたは標準出力へ書き出す export コマンドのオプションを保持します。</summary>
[Verb("export", HelpText = "Export database records to a file or stdout.")]
public sealed class ExportOptions : CommonOptions;
