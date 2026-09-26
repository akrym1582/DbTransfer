using System.Text.Json;

namespace DbTransfer.Core;

/// <summary>Atomically persists a small restart marker; transferred records are never buffered here.</summary>
public sealed class CheckpointStore(string path)
{
    public async ValueTask<TransferCheckpoint?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return await JsonSerializer.DeserializeAsync<TransferCheckpoint>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask SaveAsync(TransferCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await JsonSerializer.SerializeAsync(stream, checkpoint, cancellationToken: cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporary, fullPath, true);
    }

    public void Delete()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
