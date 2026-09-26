namespace DbTransfer.Core;

public sealed record TransferCheckpoint(long BatchesCompleted, long RowsCompleted, string? Continuation);
