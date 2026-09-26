using CommandLine;

/// <summary>ファイルまたは標準入力のレコードをデータベースへ書き込む import コマンドのオプションを保持します。</summary>
[Verb("import", HelpText = "Import records from a file or stdin.")]
public sealed class ImportOptions : CommonOptions;
