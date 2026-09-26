namespace DbTransfer.Core;

/// <summary>Controls schema mapping, destination creation, transaction scope, and restart behavior.</summary>
public sealed record DatabaseTransferOptions
{
    public required QualifiedName Destination { get; init; }

    public IReadOnlyDictionary<string, string> ColumnMappings { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool CreateTable { get; init; }

    public TransactionMode TransactionMode { get; init; } = TransactionMode.Batch;

    public bool UseNativeBulk { get; init; } = true;

    public string? CheckpointFile { get; init; }

    public bool Resume { get; init; }

    /// <summary>Gets identifies every input that affects which rows are written and how batches are formed.</summary>
    public string? PlanFingerprint { get; init; }
}
