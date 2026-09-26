using System.Runtime.CompilerServices;
using DbTransfer.Core;

namespace DbTransfer.Tests;

/// <summary>Tests durable restart state and provider composition without requiring a live database.</summary>
public sealed class DatabaseTransferTests
{
    private static readonly RecordSchema TestSchema = new([new("id", typeof(int), false)]);

    /// <summary>Checkpoint replacement is readable and deletion removes restart state.</summary>
    /// <returns>A task that completes after filesystem assertions finish.</returns>
    [Fact]
    public async Task Checkpoint_store_round_trips_atomically()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dbtransfer-{Guid.NewGuid():N}.json");
        var store = new CheckpointStore(path);
        try
        {
            await store.SaveAsync(new TransferCheckpoint(2, 25, "next", "plan"), CancellationToken.None);
            Assert.Equal(new TransferCheckpoint(2, 25, "next", "plan"), await store.LoadAsync(CancellationToken.None));
            Assert.False(File.Exists(path + ".tmp"));
            store.Delete();
            Assert.Null(await store.LoadAsync(CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    /// <summary>All supported provider names and documented aliases resolve to factories.</summary>
    /// <param name="provider">A documented provider name or alias.</param>
    [Theory]
    [InlineData("postgres")]
    [InlineData("postgresql")]
    [InlineData("sqlserver")]
    [InlineData("mssql")]
    [InlineData("mysql")]
    [InlineData("oracle")]
    public void Provider_factory_accepts_supported_names(string provider) => Assert.NotNull(ConnectorFactory.Provider(provider));

    /// <summary>The bulk adapter preserves nulls, names, types, and row order.</summary>
    [Fact]
    public void Batch_reader_streams_a_bounded_batch()
    {
        var schema = new RecordSchema([new("id", typeof(int), false), new("name", typeof(string))]);
        using var reader = new RecordBatchDataReader(new RecordBatch(schema, [[1, "a"], [2, null]], 10));
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("a", reader.GetString(1));
        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(1));
        Assert.False(reader.Read());
    }

    /// <summary>Durable checkpoints are only valid when a batch commit and its marker describe the same unit.</summary>
    /// <param name="transactionMode">The unsafe transaction scope.</param>
    /// <returns>A task that completes after validation.</returns>
    [Theory]
    [InlineData(TransactionMode.None)]
    [InlineData(TransactionMode.All)]
    public async Task Checkpoints_require_batch_transactions(TransactionMode transactionMode)
    {
        var options = Options("checkpoint", transactionMode);
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            new DatabaseTransferRunner().RunAsync(new EmptySource(), new RecordingSink(), options, new TransferOptions()));
    }

    /// <summary>A resumed create-table copy reuses the table after validating its durable plan identity.</summary>
    /// <returns>A task that completes after initialization is inspected.</returns>
    [Fact]
    public async Task Resume_reuses_existing_destination_table()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dbtransfer-{Guid.NewGuid():N}.json");
        try
        {
            await new CheckpointStore(path).SaveAsync(new TransferCheckpoint(0, 0, null, "plan"), CancellationToken.None);
            var sink = new RecordingSink();
            var options = Options(path) with { Resume = true, CreateTable = true };

            await new DatabaseTransferRunner().RunAsync(new EmptySource(), sink, options, new TransferOptions());

            Assert.NotNull(sink.InitializationOptions);
            Assert.False(sink.InitializationOptions.CreateTable);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Rows are never skipped when a checkpoint belongs to a different query or destination plan.</summary>
    /// <returns>A task that completes after mismatch validation.</returns>
    [Fact]
    public async Task Resume_rejects_a_different_transfer_plan()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dbtransfer-{Guid.NewGuid():N}.json");
        try
        {
            await new CheckpointStore(path).SaveAsync(new TransferCheckpoint(1, 10, null, "old-plan"), CancellationToken.None);
            var sink = new RecordingSink();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new DatabaseTransferRunner().RunAsync(
                    new EmptySource(), sink, Options(path) with { Resume = true }, new TransferOptions()));

            Assert.Contains("does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(sink.InitializationOptions);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Changes to query, endpoint, mapping, or batching produce a different checkpoint identity.</summary>
    [Fact]
    public void Transfer_plan_fingerprint_covers_copy_inputs()
    {
        var options = Options(null);
        var first = TransferPlanFingerprint.Create("postgres", "source", "select 1", "mysql", "destination", options, 100, 1024);
        var changedQuery = TransferPlanFingerprint.Create("postgres", "source", "select 2", "mysql", "destination", options, 100, 1024);
        var changedBatch = TransferPlanFingerprint.Create("postgres", "source", "select 1", "mysql", "destination", options, 101, 1024);

        Assert.NotEqual(first, changedQuery);
        Assert.NotEqual(first, changedBatch);
    }

    private static DatabaseTransferOptions Options(string? checkpoint, TransactionMode mode = TransactionMode.Batch) => new()
    {
        Destination = new QualifiedName("target"),
        TransactionMode = mode,
        CheckpointFile = checkpoint,
        PlanFingerprint = "plan",
    };

    private sealed class EmptySource : ISourceConnector
    {
        public ConnectorCapabilities Capabilities => ConnectorCapabilities.OrderedWrites;

        public ValueTask<RecordSchema> GetSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(TestSchema);

        public async IAsyncEnumerable<RecordBatch> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class RecordingSink : IDatabaseSink
    {
        public ConnectorCapabilities Capabilities => ConnectorCapabilities.BatchTransactions;

        public DatabaseTransferOptions? InitializationOptions { get; private set; }

        public ValueTask InitializeAsync(RecordSchema schema, DatabaseTransferOptions options, CancellationToken cancellationToken)
        {
            InitializationOptions = options;
            return ValueTask.CompletedTask;
        }

        public ValueTask ValidateAsync(RecordSchema schema, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask<WriteResult> WriteAsync(RecordBatch batch, CancellationToken cancellationToken) =>
            ValueTask.FromResult(WriteResult.Success(batch.Count));

        public ValueTask CompleteAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
