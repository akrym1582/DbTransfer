using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DbTransfer.Core;

/// <summary>Creates a stable, non-secret identifier for a database copy plan.</summary>
public static class TransferPlanFingerprint
{
    public static string Create(
        string sourceProvider,
        string sourceConnection,
        string query,
        string destinationProvider,
        string destinationConnection,
        DatabaseTransferOptions options,
        int batchSize,
        long maxBatchBytes)
    {
        ArgumentNullException.ThrowIfNull(options);
        var mappings = options.ColumnMappings.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new[] { pair.Key, pair.Value });
        var plan = new
        {
            SourceProvider = sourceProvider.Trim().ToUpperInvariant(),
            SourceConnection = sourceConnection,
            Query = query,
            DestinationProvider = destinationProvider.Trim().ToUpperInvariant(),
            DestinationConnection = destinationConnection,
            Destination = new[] { options.Destination.Catalog, options.Destination.Schema, options.Destination.Name },
            Mappings = mappings,
            options.CreateTable,
            options.TransactionMode,
            options.UseNativeBulk,
            BatchSize = batchSize,
            MaxBatchBytes = maxBatchBytes,
        };
        var json = JsonSerializer.Serialize(plan);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
