using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Provides atomic reference count operations on <see cref="Models.FileChunk"/> rows
/// using <c>ExecuteUpdateAsync</c> to issue a single UPDATE statement
/// — no prior read is needed, avoiding EF in-memory read-modify-write race conditions.
/// Falls back to EF change tracking when using InMemory provider (unit tests).
/// </summary>
internal static class ChunkReferenceHelper
{
    /// <summary>
    /// Atomically increments the reference count on a chunk by its database ID.
    /// Uses a single UPDATE statement — no prior read is needed.
    /// </summary>
    public static async Task IncrementAsync(FilesDbContext db, Guid chunkId, CancellationToken cancellationToken = default)
    {
        if (IsInMemoryProvider(db))
        {
            var chunk = await db.FileChunks.FindAsync([chunkId], cancellationToken);
            if (chunk is not null)
            {
                chunk.ReferenceCount++;
                chunk.LastReferencedAt = DateTime.UtcNow;
            }
            return;
        }

        await db.FileChunks
            .Where(c => c.Id == chunkId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.ReferenceCount, c => c.ReferenceCount + 1)
                    .SetProperty(c => c.LastReferencedAt, _ => DateTime.UtcNow),
                cancellationToken);
    }

    /// <summary>
    /// Atomically increments reference counts on multiple chunks in a single UPDATE statement.
    /// Use this instead of calling <see cref="IncrementAsync"/> in a loop to eliminate
    /// per-chunk DB round-trips during upload completion.
    /// </summary>
    public static async Task IncrementBatchAsync(FilesDbContext db, IReadOnlyList<Guid> chunkIds, CancellationToken cancellationToken = default)
    {
        if (chunkIds.Count == 0) return;

        if (IsInMemoryProvider(db))
        {
            var chunks = await db.FileChunks.Where(c => chunkIds.Contains(c.Id)).ToListAsync(cancellationToken);
            foreach (var chunk in chunks)
            {
                chunk.ReferenceCount++;
                chunk.LastReferencedAt = DateTime.UtcNow;
            }
            return;
        }

        await db.FileChunks
            .Where(c => chunkIds.Contains(c.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.ReferenceCount, c => c.ReferenceCount + 1)
                    .SetProperty(c => c.LastReferencedAt, _ => DateTime.UtcNow),
                cancellationToken);
    }

    /// <summary>
    /// Atomically decrements the reference count on a chunk by its database ID,
    /// clamping at zero to prevent negative counts.
    /// </summary>
    public static async Task DecrementAsync(FilesDbContext db, Guid chunkId, CancellationToken cancellationToken = default)
    {
        if (IsInMemoryProvider(db))
        {
            var chunk = await db.FileChunks.FindAsync([chunkId], cancellationToken);
            if (chunk is not null)
                chunk.ReferenceCount = Math.Max(0, chunk.ReferenceCount - 1);
            return;
        }

        await db.FileChunks
            .Where(c => c.Id == chunkId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.ReferenceCount, c => Math.Max(c.ReferenceCount - 1, 0))
                    .SetProperty(c => c.LastReferencedAt, _ => DateTime.UtcNow),
                cancellationToken);
    }

    /// <summary>Returns true when the context is backed by the EF InMemory provider (test scenarios).</summary>
    internal static bool IsInMemoryProvider(FilesDbContext db)
        => db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

    /// <summary>Returns true when the context is backed by PostgreSQL (Npgsql).</summary>
    internal static bool IsPostgresProvider(FilesDbContext db)
        => db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Returns true when the context is backed by SQL Server.</summary>
    internal static bool IsSqlServerProvider(FilesDbContext db)
        => db.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
}
