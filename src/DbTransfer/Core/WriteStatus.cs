namespace DbTransfer.Core;

/// <summary>接続先への書き込み結果が成功、失敗、または判定不能のいずれであるかを表します。</summary>
public enum WriteStatus
{
    /// <summary>対象レコードの書き込みが成功したことを示します。</summary>
    Succeeded,

    /// <summary>対象レコードの書き込みが失敗したことを示します。</summary>
    Failed,

    /// <summary>通信切断などにより、接続先で書き込みが確定したか判断できないことを示します。</summary>
    Unknown,
}
