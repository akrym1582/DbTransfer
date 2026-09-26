using DbTransfer.Core;

namespace DbTransfer.Connectors.PostgreSql;

/// <summary>Quotes identifiers for PostgreSQL-generated SQL.</summary>
public sealed class PostgreSqlDialect() : SqlDialectBase("\"", "\"");
