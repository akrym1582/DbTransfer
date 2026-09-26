namespace DbTransfer.Core;

/// <summary>開始引用符と終了引用符から、識別子および修飾名の共通引用処理を提供します。</summary>
/// <param name="opening">識別子の先頭に付ける、プロバイダー固有の引用符です。</param>
/// <param name="closing">識別子の末尾に付け、識別子内では二重化する引用符です。</param>
public abstract class SqlDialectBase(string opening, string closing) : ISqlDialect
{
    /// <inheritdoc/>
    public string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return opening + identifier.Replace(closing, closing + closing, StringComparison.Ordinal) + closing;
    }

    /// <inheritdoc/>
    public string QuoteName(QualifiedName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return string.Join('.', new[] { name.Catalog, name.Schema, name.Name }
            .Where(x => x is not null).Select(x => QuoteIdentifier(x!)));
    }
}
