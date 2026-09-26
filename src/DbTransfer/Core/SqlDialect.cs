namespace DbTransfer.Core;

public sealed record QualifiedName(string Name, string? Schema = null, string? Catalog = null);

public interface ISqlDialect
{
    string QuoteIdentifier(string identifier);
    string QuoteName(QualifiedName name);
}

public abstract class SqlDialectBase(string opening, string closing) : ISqlDialect
{
    public string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return opening + identifier.Replace(closing, closing + closing, StringComparison.Ordinal) + closing;
    }

    public string QuoteName(QualifiedName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return string.Join('.', new[] { name.Catalog, name.Schema, name.Name }
            .Where(x => x is not null).Select(x => QuoteIdentifier(x!)));
    }
}
