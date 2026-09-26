namespace DbTransfer.Core;

/// <summary>テーブルなどのデータベースオブジェクト名を、引用符で囲む単位に分けて保持します。</summary>
/// <param name="Name">必ず指定するオブジェクト自体の名前です。</param>
/// <param name="Schema">スキーマ名です。指定しない場合は <see langword="null"/> です。</param>
/// <param name="Catalog">データベースやカタログの名前です。指定しない場合は <see langword="null"/> です。</param>
public sealed record QualifiedName(string Name, string? Schema = null, string? Catalog = null);
