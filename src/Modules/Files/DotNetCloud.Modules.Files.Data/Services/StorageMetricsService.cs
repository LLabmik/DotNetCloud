using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Computes storage usage and content-hash deduplication metrics from the database.
/// </summary>
internal sealed class StorageMetricsService : IStorageMetricsService
{
    private readonly FilesDbContext _db;

    public StorageMetricsService(FilesDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<StorageMetricsDto> GetDeduplicationMetricsAsync(CancellationToken cancellationToken = default)
    {
        // Physical storage: unique chunks actually stored (only referenced chunks)
        var physicalBytes = await _db.FileChunks
            .Where(c => c.ReferenceCount > 0)
            .SumAsync(c => (long)c.Size, cancellationToken);

        // Logical storage: sum of all version sizes (what would be stored without dedup)
        var logicalBytes = await _db.FileVersions
            .SumAsync(v => v.Size, cancellationToken);

        var totalChunks = await _db.FileChunks
            .CountAsync(c => c.ReferenceCount > 0, cancellationToken);

        var totalVersions = await _db.FileVersions.CountAsync(cancellationToken);

        // Total non-deleted files
        var totalFiles = await _db.FileNodes
            .CountAsync(cancellationToken);

        return new StorageMetricsDto
        {
            PhysicalStorageBytes = physicalBytes,
            LogicalStorageBytes = logicalBytes,
            DeduplicationSavingsBytes = Math.Max(0, logicalBytes - physicalBytes),
            TotalUniqueChunks = totalChunks,
            TotalVersions = totalVersions,
            TotalFiles = totalFiles
        };
    }
}
