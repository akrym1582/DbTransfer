using DbTransfer.Connectors.MySql;
using DbTransfer.Connectors.PostgreSql;
using DbTransfer.Connectors.SqlServer;
using DbTransfer.Core;

namespace DbTransfer.Tests;

public sealed class CoreTests
{
    [Fact]
    public void Schema_rejects_duplicate_names() => Assert.Throws<ArgumentException>(() =>
        new RecordSchema([new("id", typeof(int)), new("id", typeof(int))]));

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
            _ => new MySqlDialect()
        };
        Assert.Equal(expected, value.QuoteName(new QualifiedName("odd]name".Replace("]", dialect == "postgres" ? "\"" : "]"), "public")));
    }

    [Fact]
    public async Task Engine_streams_bounded_batches_to_sink()
    {
        var schema = new RecordSchema([new("id", typeof(int), false)]);
        var source = new FakeSource(schema, 3);
        var sink = new FakeSink();
        var result = await new TransferEngine().RunAsync(source, sink,
            new TransferOptions { BufferBatches = 1, MemoryBudgetBytes = 10 });
        Assert.Equal(3, result.Read);
        Assert.Equal(3, result.Written);
        Assert.Equal(WriteStatus.Succeeded, result.Status);
    }

    private sealed class FakeSource(RecordSchema schema, int count) : ISourceConnector
    {
        public ConnectorCapabilities Capabilities => ConnectorCapabilities.Resume;
        public ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(schema);
        public async IAsyncEnumerable<RecordBatch> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var i = 0; i < count; i++) { yield return new RecordBatch(schema, [[i]], 10); await Task.Yield(); }
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
