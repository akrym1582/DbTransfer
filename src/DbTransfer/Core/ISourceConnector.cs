namespace DbTransfer.Core;

/// <summary>接続元からスキーマとレコードを順次読み取るための共通インターフェイスです。</summary>
public interface ISourceConnector
{
    /// <summary>Gets この接続元が、正しい結果を保証して提供できる機能を取得します。</summary>
    ConnectorCapabilities Capabilities { get; }

    /// <summary>レコードを解釈するために必要な列名、型、および順序を取得します。</summary>
    /// <param name="cancellationToken">スキーマ取得を中止する要求を受け取るトークンです。</param>
    /// <returns>接続元が返すレコードのスキーマです。</returns>
    ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken);

    /// <summary>全件をメモリに保持せず、有限個のレコードからなるバッチを順次読み取ります。</summary>
    /// <param name="cancellationToken">読み取りと列挙を中止する要求を受け取るトークンです。</param>
    /// <returns>接続元から読み取ったバッチを到着順に返す非同期シーケンスです。</returns>
    IAsyncEnumerable<RecordBatch> ReadAsync(CancellationToken cancellationToken);
}
