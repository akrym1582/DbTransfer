using CommandLine;

/// <summary>選択したプロバイダーの SQL を実行する exec コマンドのオプションを保持します。</summary>
[Verb("exec", HelpText = "Execute provider-specific SQL.")]
public sealed class ExecOptions : CommonOptions;
