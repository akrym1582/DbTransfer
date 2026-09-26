using DbTransfer.Core;

namespace DbTransfer.Connectors.Oracle;

/// <summary>Quotes identifiers for Oracle-generated SQL.</summary>
public sealed class OracleDialect() : SqlDialectBase("\"", "\"");
