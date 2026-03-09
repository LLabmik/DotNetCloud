using DotNetCloud.Modules.Files.Models;
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
    /// Fetches (or creates) the <see cref="UserSyncCounter"/> for the given user, increments
    /// <see cref="UserSyncCounter.CurrentSequence"/>, and assigns the new value to
    /// <see cref="FileNode.SyncSequence"/>.
    /// Call this before <c>SaveChangesAsync</c> for each mutated node.
    /// </summary>
    public static async Task AssignNextSequenceAsync(FilesDbContext db, FileNode node, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var counter = await db.UserSyncCounters.FindAsync([ownerId], cancellationToken);
        if (counter is null)
        {
            counter = new UserSyncCounter { UserId = ownerId };
            db.UserSyncCounters.Add(counter);
        }

        counter.CurrentSequence++;
        counter.UpdatedAt = DateTime.UtcNow;
        node.SyncSequence = counter.CurrentSequence;
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
