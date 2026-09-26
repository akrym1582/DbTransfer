using DbTransfer.Core;

namespace DbTransfer.Connectors.SqlServer;

/// <summary>Microsoft SQL Server の識別子を角括弧で囲み、名前に含まれる閉じ角括弧を二重化します。</summary>
public sealed class SqlServerDialect() : SqlDialectBase("[", "]");
