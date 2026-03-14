using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Provides atomic reference count operations on <see cref="Models.FileChunk"/> rows
/// using raw SQL to avoid EF in-memory read-modify-write race conditions.
/// All mutations use PostgreSQL row-level locking via UPDATE to guarantee correct counts
/// even under concurrent access.
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

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "FileChunks"
            SET "ReferenceCount" = "ReferenceCount" + 1,
                "LastReferencedAt" = NOW()
            WHERE "Id" = {0}
            """,
            [chunkId],
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

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "FileChunks"
            SET "ReferenceCount" = GREATEST("ReferenceCount" - 1, 0)
            WHERE "Id" = {0}
            """,
            [chunkId],
            cancellationToken);
    }

    /// <summary>Returns true when the context is backed by the EF InMemory provider (test scenarios).</summary>
    internal static bool IsInMemoryProvider(FilesDbContext db)
        => db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
}
