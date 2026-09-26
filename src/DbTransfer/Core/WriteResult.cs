namespace DbTransfer.Core;

/// <summary>一回のバッチ書き込みで確定した件数と、次回の再開に利用できる位置を表します。</summary>
/// <param name="Status">書き込み全体の結果です。</param>
/// <param name="Succeeded">接続先への反映が確認できたレコード数です。</param>
/// <param name="Failed">接続先へ反映されなかったことが確認できたレコード数です。</param>
/// <param name="Continuation">次の処理を再開する位置を表す、コネクター固有の値です。再開位置がない場合は <see langword="null"/> です。</param>
public sealed record WriteResult(WriteStatus Status, long Succeeded, long Failed = 0, string? Continuation = null)
{
    /// <summary>すべての対象レコードが成功した書き込み結果を作成します。</summary>
    /// <param name="count">接続先への反映が確認できたレコード数です。</param>
    /// <param name="continuation">次の処理を再開する位置です。再開位置がない場合は <see langword="null"/> です。</param>
    /// <returns>失敗件数が 0 で、状態が <see cref="WriteStatus.Succeeded"/> の結果です。</returns>
    public static WriteResult Success(long count, string? continuation = null) => new(WriteStatus.Succeeded, count, 0, continuation);
}
