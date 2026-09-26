using DbTransfer.Core;

namespace DbTransfer.Tests;

/// <summary>Tests durable restart state and provider composition without requiring a live database.</summary>
public sealed class DatabaseTransferTests
{
    /// <summary>Checkpoint replacement is readable and deletion removes restart state.</summary>
    /// <returns>A task that completes after filesystem assertions finish.</returns>
    [Fact]
    public async Task Checkpoint_store_round_trips_atomically()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dbtransfer-{Guid.NewGuid():N}.json");
        var store = new CheckpointStore(path);
        try
        {
            await store.SaveAsync(new TransferCheckpoint(2, 25, "next"), CancellationToken.None);
            Assert.Equal(new TransferCheckpoint(2, 25, "next"), await store.LoadAsync(CancellationToken.None));
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
}
