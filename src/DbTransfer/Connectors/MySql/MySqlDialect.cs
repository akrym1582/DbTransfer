using DbTransfer.Core;

namespace DbTransfer.Connectors.MySql;

/// <summary>MySQL の識別子をバッククォートで囲み、名前に含まれるバッククォートを二重化します。</summary>
public sealed class MySqlDialect() : SqlDialectBase("`", "`");
