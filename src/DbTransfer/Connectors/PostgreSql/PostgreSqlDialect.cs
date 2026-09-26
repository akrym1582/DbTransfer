using DbTransfer.Core;

namespace DbTransfer.Connectors.PostgreSql;

/// <summary>PostgreSQL の識別子を二重引用符で囲み、名前に含まれる二重引用符を二重化します。</summary>
public sealed class PostgreSqlDialect() : SqlDialectBase("\"", "\"");
