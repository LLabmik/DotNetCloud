namespace DotNetCloud.Core.Services;

/// <summary>
/// Dispatches a Files-module reindex request for admin shared-folder maintenance.
/// </summary>
public interface IAdminSharedFolderReindexDispatcher
{
    /// <summary>
    /// Requests a reindex of the Files module.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the reindex request was accepted; otherwise <see langword="false"/>.</returns>
    Task<bool> RequestFilesReindexAsync(CancellationToken cancellationToken = default);
}
