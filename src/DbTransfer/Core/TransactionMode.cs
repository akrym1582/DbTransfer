namespace DbTransfer.Core;

public enum TransactionMode
{
    /// <summary>Do not start a transaction.</summary>
    None,

    /// <summary>Commit each bounded batch independently.</summary>
    Batch,

    /// <summary>Commit the complete transfer atomically.</summary>
    All,
}
