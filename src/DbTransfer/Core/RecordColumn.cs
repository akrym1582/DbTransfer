using System.Collections.ObjectModel;

namespace DbTransfer.Core;

/// <summary>転送レコードの一つの列について、名前、CLR 型、および <see langword="null"/> の許容可否を定義します。</summary>
public sealed record RecordColumn
{
    /// <summary>Initializes a new instance of the <see cref="RecordColumn"/> class.転送レコードの列定義を作成します。</summary>
    /// <param name="name">接続元と接続先で列を識別する、空白ではない名前です。</param>
    /// <param name="dataType">列値を保持する CLR 型です。</param>
    /// <param name="isNullable"><see langword="null"/> を有効な列値として許容する場合は <see langword="true"/> です。</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> が空文字列または空白だけの場合に発生します。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> または <paramref name="dataType"/> が <see langword="null"/> の場合に発生します。</exception>
    public RecordColumn(string name, Type dataType, bool isNullable = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dataType);
        Name = name;
        DataType = dataType;
        IsNullable = isNullable;
    }

    /// <summary>Gets スキーマ内で列を識別する名前を取得します。</summary>
    public string Name { get; }

    /// <summary>Gets 列値を型情報を失わずに受け渡すための CLR 型を取得します。</summary>
    public Type DataType { get; }

    /// <summary>Gets a value indicating whether この列が <see langword="null"/> 値を受け入れるかどうかを取得します。</summary>
    public bool IsNullable { get; }
}
