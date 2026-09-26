using System.Collections.ObjectModel;

namespace DbTransfer.Core;

/// <summary>同じスキーマを共有し、一回の読み書きで扱える有限個のレコードを保持します。</summary>
public sealed class RecordBatch
{
    private readonly object?[][] _rows;

    /// <summary>Initializes a new instance of the <see cref="RecordBatch"/> class.各行を複製して、呼び出し元による配列変更の影響を受けないバッチを作成します。</summary>
    /// <param name="schema">各行の値の意味と順序を定義するスキーマです。</param>
    /// <param name="rows">スキーマの列数と同じ長さを持つ値配列のシーケンスです。</param>
    /// <param name="estimatedBytes">バックプレッシャーの判定に使うペイロードサイズの見積もりです。厳密なメモリ使用量である必要はありません。</param>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> または <paramref name="rows"/> が <see langword="null"/> の場合に発生します。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="estimatedBytes"/> が負の場合に発生します。</exception>
    /// <exception cref="ArgumentException">いずれかの行の値の個数がスキーマの列数と一致しない場合に発生します。</exception>
    public RecordBatch(RecordSchema schema, IEnumerable<object?[]> rows, long estimatedBytes)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedBytes);

        Schema = schema;
        _rows = [.. rows.Select(row =>
        {
            if (row.Length != schema.Count)
            {
                throw new ArgumentException("A row does not match the schema width.", nameof(rows));
            }

            return (object?[])row.Clone();
        })];
        EstimatedBytes = estimatedBytes;
    }

    /// <summary>Gets すべての行に共通する列定義を取得します。</summary>
    public RecordSchema Schema { get; }

    /// <summary>Gets スキーマの列順に値が格納された行を取得します。</summary>
    public IReadOnlyList<object?[]> Rows => _rows;

    /// <summary>Gets このバッチに含まれる行数を取得します。</summary>
    public int Count => _rows.Length;

    /// <summary>Gets バッファーのバイト上限を守るために使用する、ペイロードサイズの見積もりを取得します。</summary>
    public long EstimatedBytes { get; }
}
