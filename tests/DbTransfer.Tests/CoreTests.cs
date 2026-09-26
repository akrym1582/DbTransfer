using DbTransfer.Connectors.MySql;
using DbTransfer.Connectors.PostgreSql;
using DbTransfer.Connectors.SqlServer;
using DbTransfer.Core;

namespace DbTransfer.Tests;

/// <summary>
/// 転送の中心となるデータ構造、SQL 方言、および転送エンジンを組み合わせて検証します。
/// </summary>
public sealed class CoreTests
{
    /// <summary>
    /// 列名から値を一意に取得できなくなる重複スキーマが拒否されることを確認します。
    /// </summary>
    [Fact]
    public void Schema_rejects_duplicate_names() => Assert.Throws<ArgumentException>(() =>
        new RecordSchema([new("id", typeof(int)), new("id", typeof(int))]));

    /// <summary>
    /// DB ごとの引用符とエスケープ規則が、スキーマ名とテーブル名の両方へ適用されることを確認します。
    /// </summary>
    /// <param name="dialect">検証するデータベース方言を識別するテスト用の名前です。</param>
    /// <param name="expected">引用符とエスケープを適用した後に得られる識別子です。</param>
    [Theory]
    [InlineData("postgres", "\"public\".\"odd\"\"name\"")]
    [InlineData("sqlserver", "[public].[odd]]name]")]
    [InlineData("mysql", "`public`.`odd]name`")]
    public void Dialects_quote_each_name_component(string dialect, string expected)
    {
        ISqlDialect value = dialect switch
        {
            "postgres" => new PostgreSqlDialect(),
            "sqlserver" => new SqlServerDialect(),
            _ => new MySqlDialect(),
        };

        Assert.Equal(
            expected,
            value.QuoteName(new QualifiedName("odd]name".Replace("]", dialect == "postgres" ? "\"" : "]"), "public")));
    }

    /// <summary>
    /// 小さいバッファでもバックプレッシャーを維持し、すべてのバッチを sink へ渡せることを確認します。
    /// </summary>
    /// <returns>非同期転送と検証が完了した時点で終了するタスクです。</returns>
    [Fact]
    public async Task Engine_streams_bounded_batches_to_sink()
    {
        var schema = new RecordSchema([new("id", typeof(int), false)]);
        var source = new FakeSource(schema, 3);
        var sink = new FakeSink();
        var result = await new TransferEngine().RunAsync(
            source,
            sink,
            new TransferOptions { BufferBatches = 1, MemoryBudgetBytes = 10 });

        Assert.Equal(3, result.Read);
        Assert.Equal(3, result.Written);
        Assert.Equal(WriteStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task Engine_reports_cumulative_progress_after_each_written_batch()
    {
        var schema = new RecordSchema([new("id", typeof(int), false)]);
        var reports = new List<(long Read, long Written)>();
        await new TransferEngine().RunAsync(
            new FakeSource(schema, 3),
            new FakeSink(),
            new TransferOptions
            {
                BufferBatches = 1,
                MemoryBudgetBytes = 10,
                Progress = (read, written) => reports.Add((read, written)),
            });

        Assert.Equal(3, reports.Count);
        Assert.Equal(3, reports[^1].Written);
        Assert.All(reports, report => Assert.True(report.Read >= report.Written));
    }

    private sealed class FakeSource(RecordSchema schema, int count) : ISourceConnector
    {
        public ConnectorCapabilities Capabilities => ConnectorCapabilities.Resume;

        public ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(schema);

        public async IAsyncEnumerable<RecordBatch> ReadAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var i = 0; i < count; i++)
            {
                yield return new RecordBatch(schema, [[i]], 10);
                await Task.Yield();
            }
        }
    }

    private sealed class FakeSink : ISinkConnector
    {
        public ConnectorCapabilities Capabilities => ConnectorCapabilities.NativeBulk;

        public ValueTask ValidateAsync(RecordSchema schema, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask<WriteResult> WriteAsync(RecordBatch batch, CancellationToken cancellationToken) =>
            ValueTask.FromResult(WriteResult.Success(batch.Count));
    }
}
