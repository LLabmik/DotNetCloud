using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

internal static class MountedWriteAccessGuard
{
    private const string ReadOnlyMessage = "Mounted shared folders are read-only in this first delivery.";

    internal static Task EnsureWritableNodeAsync(FilesDbContext db, Guid nodeId, CancellationToken cancellationToken)
        => EnsureWritableNodeCoreAsync(db, nodeId, cancellationToken);

    internal static Task EnsureWritableFolderAsync(FilesDbContext db, Guid? nodeId, CancellationToken cancellationToken)
        => nodeId.HasValue
            ? EnsureWritableNodeCoreAsync(db, nodeId.Value, cancellationToken)
            : Task.CompletedTask;

    private static async Task EnsureWritableNodeCoreAsync(FilesDbContext db, Guid nodeId, CancellationToken cancellationToken)
    {
        if (!await IsMountedNodeAsync(db, nodeId, cancellationToken))
        {
            return;
        }

        throw new Core.Errors.InvalidOperationException(ReadOnlyMessage);
    }

    private static async Task<bool> IsMountedNodeAsync(FilesDbContext db, Guid nodeId, CancellationToken cancellationToken)
    {
        if (nodeId == VirtualMountedNodeRegistry.DotNetCloudRootId || nodeId == VirtualMountedNodeRegistry.SharedWithMeRootId)
        {
            return true;
        }

        if (VirtualMountedNodeRegistry.TryGet(nodeId, out _))
        {
            return true;
        }

        var sharedFolderIds = await db.AdminSharedFolders
            .AsNoTracking()
            .Select(definition => definition.Id)
            .ToListAsync(cancellationToken);

        return sharedFolderIds.Any(sharedFolderId => VirtualMountedNodeRegistry.GetAdminSharedFolderRootId(sharedFolderId) == nodeId);
    }
}