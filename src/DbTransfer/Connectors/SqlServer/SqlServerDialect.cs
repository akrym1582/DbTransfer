using DbTransfer.Core;

namespace DbTransfer.Connectors.SqlServer;

/// <summary>Quotes identifiers for Microsoft SQL Server-generated SQL.</summary>
public sealed class SqlServerDialect() : SqlDialectBase("[", "]");
