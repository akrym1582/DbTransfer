using System.Threading.Channels;

namespace DbTransfer.Core;

/// <summary>転送元から読み取った件数と、転送先で反映を確認できた件数を区別して報告します。</summary>
/// <param name="Read">転送元からバッチとして読み取り済みのレコード数です。</param>
/// <param name="Written">転送先への反映が確認できたレコード数です。</param>
/// <param name="Status">転送先が返した最終的な書き込み状態です。</param>
public sealed record TransferResult(long Read, long Written, WriteStatus Status);
