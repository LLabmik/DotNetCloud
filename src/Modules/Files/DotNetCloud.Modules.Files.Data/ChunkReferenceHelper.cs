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
}
