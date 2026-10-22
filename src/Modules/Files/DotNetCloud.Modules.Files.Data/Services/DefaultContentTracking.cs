using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Keeps the default-content tracking rows in sync with user mutations and identifies
/// starter content that automatic trash retention must never purge.
/// </summary>
internal static class DefaultContentTracking
{
    /// <summary>
    /// Marks tracked items whose node was trashed or permanently deleted as
    /// <see cref="DefaultContentItemState.RemovedByUser"/>. The tombstone keeps the item from
    /// being recreated by a later convergence pass.
    /// </summary>
    public static async Task MarkRemovedAsync(
        FilesDbContext db,
        IReadOnlyCollection<Guid> nodeIds,
        CancellationToken cancellationToken = default)
    {
        if (nodeIds.Count == 0)
            return;

        var rows = await db.UserDefaultContentItems
            .Where(i => i.FileNodeId != null && nodeIds.Contains(i.FileNodeId.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.State = DefaultContentItemState.RemovedByUser;
            row.LastCheckedAtUtc = now;

            // Keep the node id while the node still exists (trash) so restore can find it again.
            // A permanent purge clears it in MarkPurgedAsync.
        }
    }

    /// <summary>
    /// Clears the node link for tracked items whose node was permanently deleted.
    /// </summary>
    public static async Task MarkPurgedAsync(
        FilesDbContext db,
        IReadOnlyCollection<Guid> nodeIds,
        CancellationToken cancellationToken = default)
    {
        if (nodeIds.Count == 0)
            return;

        var rows = await db.UserDefaultContentItems
            .Where(i => i.FileNodeId != null && nodeIds.Contains(i.FileNodeId.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.State = DefaultContentItemState.RemovedByUser;
            row.FileNodeId = null;
            row.LastCheckedAtUtc = now;
        }
    }

    /// <summary>
    /// Reclassifies tracked items after a restore: <see cref="DefaultContentItemState.Present"/>
    /// when the content still matches what was provisioned, otherwise
    /// <see cref="DefaultContentItemState.ChangedByUser"/>.
    /// </summary>
    public static async Task MarkRestoredAsync(
        FilesDbContext db,
        IReadOnlyCollection<FileNode> nodes,
        CancellationToken cancellationToken = default)
    {
        if (nodes.Count == 0)
            return;

        var ids = nodes.Select(n => n.Id).ToHashSet();
        var rows = await db.UserDefaultContentItems
            .Where(i => i.FileNodeId != null && ids.Contains(i.FileNodeId.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byId = nodes.ToDictionary(n => n.Id);
        var now = DateTime.UtcNow;

        foreach (var row in rows)
        {
            if (row.FileNodeId is not Guid nodeId || !byId.TryGetValue(nodeId, out var node))
                continue;

            row.State = string.Equals(row.ProvisionedContentHash, node.ContentHash, StringComparison.OrdinalIgnoreCase)
                ? DefaultContentItemState.Present
                : DefaultContentItemState.ChangedByUser;
            row.LastCheckedAtUtc = now;
        }
    }

    /// <summary>
    /// Returns the IDs of tracked starter nodes and every ancestor folder, so automatic trash
    /// retention and folder purges never remove starter content or the folders containing it.
    /// </summary>
    public static async Task<HashSet<Guid>> GetProtectedNodeIdsAsync(
        FilesDbContext db,
        CancellationToken cancellationToken = default)
    {
        var trackedIds = await db.UserDefaultContentItems
            .AsNoTracking()
            .Where(i => i.FileNodeId != null)
            .Select(i => i.FileNodeId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var protectedIds = new HashSet<Guid>(trackedIds);
        if (trackedIds.Count == 0)
            return protectedIds;

        // The materialized path is /id/id/id — every segment is an ancestor folder.
        var paths = await db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => trackedIds.Contains(n.Id))
            .Select(n => n.MaterializedPath)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var path in paths)
        {
            foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Guid.TryParse(segment, out var id))
                    protectedIds.Add(id);
            }
        }

        return protectedIds;
    }
}
