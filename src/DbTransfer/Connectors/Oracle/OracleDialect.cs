using DbTransfer.Core;

namespace DbTransfer.Connectors.Oracle;

/// <summary>Oracle の識別子を二重引用符で囲み、名前に含まれる二重引用符を二重化します。</summary>
public sealed class OracleDialect() : SqlDialectBase("\"", "\"");
