using DotNetCloud.Core.Errors;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Rejects content mutations on administrator-managed default content.
/// </summary>
/// <remarks>
/// Allowed operations are intentionally not guarded: delete/trash, copy, share, tag, comment,
/// favorite and download. Only content changes (edit, replace, version restore) and structural
/// changes (rename, move) are rejected.
/// </remarks>
public static class ReadOnlyContentGuard
{
    /// <summary>
    /// Throws <see cref="FileReadOnlyException"/> when <paramref name="nodeId"/> refers to a
    /// read-only default-content node.
    /// </summary>
    /// <param name="db">Files database context.</param>
    /// <param name="nodeId">Node to check (null means "creating a new node" and is always allowed).</param>
    /// <param name="operation">Operation name recorded on the exception.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task EnsureWritableAsync(
        FilesDbContext db,
        Guid? nodeId,
        string operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (nodeId is null || nodeId.Value == Guid.Empty)
            return;

        var isReadOnly = await db.FileNodes
            .AsNoTracking()
            .Where(n => n.Id == nodeId.Value)
            .Select(n => (bool?)n.IsReadOnly)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (isReadOnly == true)
            throw new FileReadOnlyException(operation);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the node is read-only default content.
    /// </summary>
    /// <param name="db">Files database context.</param>
    /// <param name="nodeId">Node to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<bool> IsReadOnlyAsync(
        FilesDbContext db,
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.FileNodes
            .AsNoTracking()
            .Where(n => n.Id == nodeId)
            .Select(n => n.IsReadOnly)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
