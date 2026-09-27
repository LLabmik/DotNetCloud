using DotNetCloud.Modules.Files.Options;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Applies the version retention policy to a single file: keeps at most
/// <see cref="VersionRetentionOptions.MaxVersionCount"/> versions (one when versioning is disabled)
/// and drops unlabeled versions older than <see cref="VersionRetentionOptions.RetentionDays"/>.
/// </summary>
/// <remarks>
/// <para>Shared by the scheduled retention pass and by the write paths that create a version
/// (upload completion, WOPI save, restore), so the limits are enforced as the file changes instead
/// of only on the next daily cleanup.</para>
/// <para>Two invariants are always honoured: the newest version is never removed (it is the content
/// the file currently serves) and labeled versions are never auto-deleted.</para>
/// <para>Changes are tracked but not saved — the caller owns the transaction it is already in.</para>
/// </remarks>
internal static class VersionRetentionEnforcer
{
    /// <summary>
    /// Prunes the versions of <paramref name="fileNodeId"/> that fall outside the policy.
    /// </summary>
    /// <param name="db">Database context that owns the file (tracked, not saved).</param>
    /// <param name="fileNodeId">File whose version history is evaluated.</param>
    /// <param name="options">Effective policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of versions scheduled for deletion.</returns>
    public static async Task<int> ApplyAsync(
        FilesDbContext db,
        Guid fileNodeId,
        VersionRetentionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(options);

        var maxVersionCount = FileVersioningAdminSettings.EffectiveMaxVersionCount(options);

        if (maxVersionCount <= 0 && options.RetentionDays <= 0)
        {
            return 0; // Unlimited history and no age policy — nothing to do.
        }

        // Load all versions for this file, oldest first.
        var versions = await db.FileVersions
            .Where(v => v.FileNodeId == fileNodeId)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        // Always keep at least one version.
        if (versions.Count <= 1)
        {
            return 0;
        }

        var toDeleteIds = new HashSet<Guid>();

        // Policy 1: max version count — delete oldest unlabeled versions.
        if (maxVersionCount > 0 && versions.Count > maxVersionCount)
        {
            var excess = versions.Count - maxVersionCount;
            foreach (var version in versions.Where(v => v.Label is null).Take(excess))
            {
                toDeleteIds.Add(version.Id);
            }
        }

        // Policy 2: time-based retention — delete unlabeled versions older than RetentionDays.
        if (options.RetentionDays > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-options.RetentionDays);
            foreach (var version in versions.Where(v => v.CreatedAt < cutoff && v.Label is null))
            {
                toDeleteIds.Add(version.Id);
            }
        }

        if (toDeleteIds.Count == 0)
        {
            return 0;
        }

        // Safety: the newest version is the content the file currently serves, so it is never
        // removed — not even when labeled versions fill the whole budget (e.g. versioning switched
        // off, or a count limit smaller than the number of labeled versions).
        toDeleteIds.Remove(versions[^1].Id);

        if (toDeleteIds.Count == 0)
        {
            return 0;
        }

        var versionsToDelete = versions.Where(v => toDeleteIds.Contains(v.Id)).ToList();

        foreach (var version in versionsToDelete)
        {
            // Decrement chunk reference counts.
            var versionChunks = await db.FileVersionChunks
                .Where(vc => vc.FileVersionId == version.Id)
                .ToListAsync(cancellationToken);

            foreach (var versionChunk in versionChunks)
            {
                await ChunkReferenceHelper.DecrementAsync(db, versionChunk.FileChunkId, cancellationToken);
            }

            db.FileVersionChunks.RemoveRange(versionChunks);
            db.FileVersions.Remove(version);
        }

        return versionsToDelete.Count;
    }
}
