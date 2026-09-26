using System.Text;
using DbTransfer.Formats;

namespace DbTransfer.Tests;

/// <summary>
/// JSON Lines のストリーミング処理とレコード上限の境界条件を検証します。
/// </summary>
public sealed class JsonLinesTests
{
    /// <summary>
    /// 空行はレコードに数えず、<see cref="long"/>を超える整数も JSON の元の桁を失わないことを確認します。
    /// </summary>
    /// <returns>入力ストリームを最後まで検証した時点で終了するタスクです。</returns>
    [Fact]
    public async Task Reader_skips_empty_lines_and_preserves_large_integer()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\n{\"id\":12345678901234567890}\n"));
        var records = new List<System.Text.Json.JsonElement>();
        await foreach (var record in JsonLines.ReadAsync(stream))
        {
            records.Add(record);
        }

        Assert.Single(records);
        Assert.Equal("12345678901234567890", records[0].GetProperty("id").GetRawText());
    }

    /// <summary>
    /// 不正な JSON を報告するとき、空行を含む実際の入力行番号が例外に含まれることを確認します。
    /// </summary>
    /// <returns>例外の内容を検証した時点で終了するタスクです。</returns>
    [Fact]
    public async Task Reader_reports_line_for_invalid_json()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\nnot-json\n"));
        var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var unused in JsonLines.ReadAsync(stream))
            {
            }
        });

        Assert.Contains("line 2", exception.Message);
    }

    /// <summary>
    /// 巨大な空白行もサイズ制限の対象となり、行全体をメモリへ読む前に拒否できることを確認します。
    /// </summary>
    /// <returns>読み取り位置と例外の内容を検証した時点で終了するタスクです。</returns>
    [Fact]
    public async Task Reader_rejects_oversized_whitespace_line_without_reading_the_whole_line()
    {
        var input = new string(' ', 100_000) + "\n{\"id\":1}\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var unused in JsonLines.ReadAsync(stream, maxRecordBytes: 16))
            {
            }
        });

        Assert.Contains("record 1", exception.Message);
        Assert.True(stream.Position < stream.Length);
    }

    /// <summary>
    /// レコード上限が文字数ではなく UTF-8 のバイト数として判定されることを確認します。
    /// </summary>
    /// <returns>上限直前の成功と上限超過の失敗を確認した時点で終了するタスクです。</returns>
    [Fact]
    public async Task Reader_applies_limit_to_utf8_bytes()
    {
        await using var accepted = new MemoryStream(Encoding.UTF8.GetBytes("{\"v\":\"é\"}\n"));
        await using var rejected = new MemoryStream(Encoding.UTF8.GetBytes("{\"v\":\"é\"}\n"));

        await foreach (var unused in JsonLines.ReadAsync(accepted, maxRecordBytes: 10))
        {
        }

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var unused in JsonLines.ReadAsync(rejected, maxRecordBytes: 9))
            {
            }
        });
    }
}
