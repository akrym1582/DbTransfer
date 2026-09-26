using System.Threading.Channels;

namespace DbTransfer.Core;

/// <summary>パイプラインが先読みできるバッチ数と、バッファーに使用できる推定バイト数を制限します。</summary>
public sealed record TransferOptions
{
    /// <summary>Gets 読み取り段階と書き込み段階の間で待機できる最大バッチ数を取得または初期化します。</summary>
    public int BufferBatches { get; init; } = 2;

    /// <summary>Gets 待機中の全バッチに許可する、推定ペイロードサイズの合計上限を取得または初期化します。</summary>
    public long MemoryBudgetBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>両方の上限が 1 以上であり、有限のバッファーとして機能することを検証します。</summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="BufferBatches"/> または <see cref="MemoryBudgetBytes"/> が 1 未満の場合に発生します。</exception>
    public void Validate()
    {
        if (BufferBatches < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(BufferBatches));
        }

        if (MemoryBudgetBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MemoryBudgetBytes));
        }
    }
}
