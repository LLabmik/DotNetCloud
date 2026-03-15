using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Provides atomic sync-sequence assignment for file mutations.
/// Used by <see cref="Services.FileService"/>, <see cref="Services.ChunkedUploadService"/>,
/// and <see cref="Services.TrashService"/> to stamp each mutated <see cref="FileNode"/> with
/// a monotonically increasing <see cref="FileNode.SyncSequence"/> for cursor-based delta sync.
/// </summary>
internal static class SyncCursorHelper
{
    /// <summary>
    /// Atomically increments the <see cref="UserSyncCounter"/> for the given user using a raw SQL
    /// upsert with <c>RETURNING</c>, and assigns the new value to <see cref="FileNode.SyncSequence"/>.
    /// This guarantees sequential, gap-free sequence numbers even under concurrent access
    /// by using PostgreSQL row-level locking via <c>INSERT ... ON CONFLICT DO UPDATE ... RETURNING</c>.
    /// Falls back to EF change tracking when using InMemory provider (unit tests).
    /// Call this before <c>SaveChangesAsync</c> for each mutated node.
    /// When <paramref name="notifier"/> is provided, publishes a change notification after assignment.
    /// </summary>
    public static async Task AssignNextSequenceAsync(FilesDbContext db, FileNode node, Guid ownerId, ISyncChangeNotifier? notifier = null, CancellationToken cancellationToken = default)
    {
        if (ChunkReferenceHelper.IsInMemoryProvider(db))
        {
            // Fallback for InMemory provider (unit tests) — not safe under concurrency.
            var counter = await db.UserSyncCounters.FindAsync([ownerId], cancellationToken);
            if (counter is null)
            {
                counter = new UserSyncCounter { UserId = ownerId };
                db.UserSyncCounters.Add(counter);
            }

            counter.CurrentSequence++;
            counter.UpdatedAt = DateTime.UtcNow;
            node.SyncSequence = counter.CurrentSequence;

            if (notifier is not null)
                await notifier.NotifyAsync(ownerId, counter.CurrentSequence, cancellationToken);

            return;
        }

        // Atomic upsert: inserts with sequence=1 if no row exists, otherwise increments.
        // PostgreSQL's INSERT ... ON CONFLICT DO UPDATE acquires a row-level lock, preventing
        // concurrent reads of the same value. RETURNING gives us the post-increment value.
        // Note: EF Core considers RETURNING-based SQL non-composable, so we materialize with
        // ToListAsync first — .SingleAsync() would fail with InvalidOperationException.
        var nextSequence = (await db.Database.SqlQueryRaw<long>(
            """
            INSERT INTO "UserSyncCounters" ("UserId", "CurrentSequence", "UpdatedAt")
            VALUES ({0}, 1, NOW())
            ON CONFLICT ("UserId") DO UPDATE
            SET "CurrentSequence" = "UserSyncCounters"."CurrentSequence" + 1,
                "UpdatedAt" = NOW()
            RETURNING "CurrentSequence"
            """,
            ownerId
        ).ToListAsync(cancellationToken)).Single();

        node.SyncSequence = nextSequence;

        // Detach any tracked UserSyncCounter entity for this user so EF doesn't try to
        // overwrite the value we just set atomically via raw SQL.
        var tracked = db.ChangeTracker.Entries<UserSyncCounter>()
            .FirstOrDefault(e => e.Entity.UserId == ownerId);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        // Notify SSE subscribers that a new change is available for this user.
        if (notifier is not null)
            await notifier.NotifyAsync(ownerId, nextSequence, cancellationToken);
    }

    /// <summary>
    /// Encodes a user ID and sequence number into a cursor string suitable for HTTP query params.
    /// Format: base64url( "{userId}:{sequence}" )
    /// </summary>
    public static string EncodeCursor(Guid userId, long sequence)
    {
        var raw = $"{userId}:{sequence}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Decodes a cursor string. Returns <c>null</c> if malformed.
    /// </summary>
    public static (Guid UserId, long Sequence)? DecodeCursor(string cursor)
    {
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var colon = raw.IndexOf(':');
            if (colon < 1) return null;
            if (!Guid.TryParse(raw[..colon], out var userId)) return null;
            if (!long.TryParse(raw[(colon + 1)..], out var sequence)) return null;
            return (userId, sequence);
        }
        catch
        {
            return null;
        }
    }
}
