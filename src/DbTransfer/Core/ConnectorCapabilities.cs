namespace DbTransfer.Core;

/// <summary>
/// コネクターが正しい結果を保証したうえで提供できる転送機能を表します。
/// 転送を始める前に、利用者が要求した処理を接続先が実行できるか判定するために使用します。
/// </summary>
[Flags]
public enum ConnectorCapabilities
{
    /// <summary>追加の転送機能を提供しないことを示します。</summary>
    None = 0,

    /// <summary>プロバイダー固有の一括書き込み API を利用できることを示します。</summary>
    NativeBulk = 1,

    /// <summary>キーが一致する行を更新し、一致しない行を追加できることを示します。</summary>
    Upsert = 2,

    /// <summary>一つのバッチに含まれる書き込みを、一つのトランザクションとして確定または取り消せることを示します。</summary>
    BatchTransactions = 4,

    /// <summary>転送全体の書き込みを、一つのトランザクションとして確定または取り消せることを示します。</summary>
    AllTransaction = 8,

    /// <summary>バッチの一部だけが成功した場合に、成功件数と失敗件数を区別して報告できることを示します。</summary>
    PartialSuccess = 16,

    /// <summary>継続位置を保存し、中断した転送をその位置から再開できることを示します。</summary>
    Resume = 32,

    /// <summary>入力されたレコードの順序を保って接続先へ書き込めることを示します。</summary>
    OrderedWrites = 64,
}
