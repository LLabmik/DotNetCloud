using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read-only access to the user's Files module directory for cross-module
/// features such as browsing and selecting files from the email compose form.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability tier:</b> Public — automatically granted to all modules.
/// </para>
/// <para>
/// This capability exposes a minimal read-only view of the user's files.
/// Modules that need to create or modify files must use the Files module API directly.
/// </para>
/// <para>
/// <b>Optional module:</b> The Files module may not be installed. Callers should
/// handle null results gracefully.
/// </para>
/// </remarks>
public interface IFileDirectory : ICapabilityInterface
{
    /// <summary>
    /// Lists the children of a folder (or root-level files if <paramref name="parentId"/> is null).
    /// </summary>
    /// <param name="userId">The user whose files to list.</param>
    /// <param name="parentId">Optional parent folder ID. Null lists root-level files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of file/folder info items.</returns>
    Task<IReadOnlyList<FileNodeInfo>> ListChildrenAsync(
        Guid userId,
        Guid? parentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for a single file or folder by its node ID.
    /// </summary>
    /// <param name="userId">The user who owns the file.</param>
    /// <param name="fileNodeId">The file/folder node ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file info if found; otherwise <c>null</c>.</returns>
    Task<FileNodeInfo?> GetFileInfoAsync(
        Guid userId,
        Guid fileNodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read stream for the latest version of a file.
    /// </summary>
    /// <param name="userId">The user who owns the file.</param>
    /// <param name="fileNodeId">The file node ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read stream if found and accessible; otherwise <c>null</c>.</returns>
    Task<Stream?> OpenReadAsync(
        Guid userId,
        Guid fileNodeId,
        CancellationToken cancellationToken = default);
}
