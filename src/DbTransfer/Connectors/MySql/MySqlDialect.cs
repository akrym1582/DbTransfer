using DbTransfer.Core;

namespace DbTransfer.Connectors.MySql;

/// <summary>Quotes identifiers for MySQL-generated SQL.</summary>
public sealed class MySqlDialect() : SqlDialectBase("`", "`");
