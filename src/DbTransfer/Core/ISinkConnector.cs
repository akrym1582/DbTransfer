namespace DbTransfer.Core;

/// <summary>転送先を事前検証し、レコードのバッチを順次書き込むための共通インターフェイスです。</summary>
public interface ISinkConnector
{
    /// <summary>Gets この転送先が、正しい結果を保証して提供できる機能を取得します。</summary>
    ConnectorCapabilities Capabilities { get; }

    /// <summary>書き込みを開始する前に、転送元のスキーマを転送先で扱えるか検証します。</summary>
    /// <param name="schema">転送元が返す列名、型、および順序です。</param>
    /// <param name="cancellationToken">検証を中止する要求を受け取るトークンです。</param>
    /// <returns>検証処理を表す値です。</returns>
    ValueTask ValidateAsync(RecordSchema schema, CancellationToken cancellationToken);

    /// <summary>一つのバッチを書き込み、接続先で確定した成功件数と失敗件数を返します。</summary>
    /// <param name="batch">同じスキーマを共有する、有限個のレコードです。</param>
    /// <param name="cancellationToken">書き込みを中止する要求を受け取るトークンです。</param>
    /// <returns>接続先で確認できた書き込み結果です。</returns>
    ValueTask<WriteResult> WriteAsync(RecordBatch batch, CancellationToken cancellationToken);
}
