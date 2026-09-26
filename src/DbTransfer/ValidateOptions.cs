using CommandLine;

/// <summary>データを書き込まずに転送設定と互換性を確認する validate コマンドのオプションを保持します。</summary>
[Verb("validate", HelpText = "Validate a transfer without writing data.")]
public sealed class ValidateOptions : CommonOptions;
