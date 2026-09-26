namespace DbTransfer.Core;

/// <summary>識別子をデータ値とは区別して安全に SQL 文へ組み込むための、プロバイダー固有の引用規則を定義します。</summary>
public interface ISqlDialect
{
    /// <summary>一つの識別子を引用符で囲み、識別子内の閉じ引用符をプロバイダーの規則に従ってエスケープします。</summary>
    /// <param name="identifier">列名やテーブル名など、分割されていない一つの識別子です。</param>
    /// <returns>生成する SQL 文へ識別子として埋め込める文字列です。</returns>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> が空文字列または空白だけの場合に発生します。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="identifier"/> が <see langword="null"/> の場合に発生します。</exception>
    string QuoteIdentifier(string identifier);

    /// <summary>カタログ、スキーマ、名前の各構成要素を個別に引用し、ピリオドで連結します。</summary>
    /// <param name="name">任意のカタログとスキーマ、および必須のオブジェクト名です。</param>
    /// <returns>生成する SQL 文へ修飾名として埋め込める文字列です。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> が <see langword="null"/> の場合に発生します。</exception>
    string QuoteName(QualifiedName name);
}
