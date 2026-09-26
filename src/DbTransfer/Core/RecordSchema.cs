using System.Collections.ObjectModel;

namespace DbTransfer.Core;

/// <summary>レコードを構成する列の順序と、列名から位置を検索する規則を保持します。</summary>
public sealed class RecordSchema
{
    private readonly ReadOnlyCollection<RecordColumn> _columns;
    private readonly Dictionary<string, int> _ordinals;

    /// <summary>Initializes a new instance of the <see cref="RecordSchema"/> class.列の並び順を維持したスキーマを作成します。</summary>
    /// <param name="columns">一つ以上の列定義です。列挙された順序が各レコード内の値の順序になります。</param>
    /// <param name="comparer">列名の比較規則です。省略した場合は、大文字と小文字を区別する序数比較を使用します。</param>
    /// <exception cref="ArgumentNullException"><paramref name="columns"/> が <see langword="null"/> の場合に発生します。</exception>
    /// <exception cref="ArgumentException">列が一つもない場合、または比較規則上で同じ名前の列が複数ある場合に発生します。</exception>
    public RecordSchema(IEnumerable<RecordColumn> columns, StringComparer? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        comparer ??= StringComparer.Ordinal;
        var materialized = columns.ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("A schema must have at least one column.", nameof(columns));
        }

        _ordinals = new Dictionary<string, int>(comparer);
        for (var i = 0; i < materialized.Length; i++)
        {
            if (!_ordinals.TryAdd(materialized[i].Name, i))
            {
                throw new ArgumentException($"Duplicate column name '{materialized[i].Name}'.", nameof(columns));
            }
        }

        _columns = Array.AsReadOnly(materialized);
    }

    /// <summary>Gets 各レコードの値と同じ順序で並んだ、変更できない列定義を取得します。</summary>
    public IReadOnlyList<RecordColumn> Columns => _columns;

    /// <summary>Gets 一つのレコードに必要な値の個数を取得します。</summary>
    public int Count => _columns.Count;

    /// <summary>列名に対応する、0 から始まるレコード内の位置を取得します。</summary>
    /// <param name="name">検索する列名です。コンストラクターに指定した比較規則が適用されます。</param>
    /// <returns>レコードの値配列で列値を取得するための位置です。</returns>
    /// <exception cref="KeyNotFoundException">指定した名前の列がスキーマに存在しない場合に発生します。</exception>
    public int GetOrdinal(string name) => _ordinals.TryGetValue(name, out var ordinal)
        ? ordinal : throw new KeyNotFoundException($"Column '{name}' was not found.");
}
