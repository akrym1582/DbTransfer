using CommandLine;

/// <summary>すべてのコマンドで共通して使用する、ジョブ定義とバッファー上限のオプションを保持します。</summary>
public abstract class CommonOptions
{
    /// <summary>Gets 接続先やマッピングなど、再利用する転送設定を記述したジョブ定義ファイルのパスを取得または初期化します。</summary>
    [Option("job", HelpText = "Path to a job definition.")]
    public string? Job { get; init; }

    /// <summary>Gets 接続先へ一度に渡すレコード数の上限を取得または初期化します。</summary>
    [Option("batch-size", Default = 1000, HelpText = "Maximum number of records in a batch.")]
    public int BatchSize { get; init; }

    /// <summary>Gets 一つのバッチに許可する、レコードの推定ペイロードサイズの上限をバイト単位で取得または初期化します。</summary>
    [Option("max-batch-bytes", Default = 4 * 1024 * 1024, HelpText = "Maximum estimated payload bytes in a batch.")]
    public long MaxBatchBytes { get; init; }

    /// <summary>Gets 読み取り段階と書き込み段階の間で待機できる最大バッチ数を取得または初期化します。</summary>
    [Option("buffer-batches", Default = 2, HelpText = "Maximum number of batches buffered between pipeline stages.")]
    public int BufferBatches { get; init; }

    /// <summary>Gets 待機中の全バッチに許可するメモリ予算を MiB 単位で取得または初期化します。</summary>
    [Option("memory-budget-mb", Default = 64, HelpText = "Memory budget in MiB for buffered record batches.")]
    public int MemoryBudgetMb { get; init; }
}
