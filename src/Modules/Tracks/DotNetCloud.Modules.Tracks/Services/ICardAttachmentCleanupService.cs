namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Clears file references from card attachments when the referenced file is deleted.
/// Implemented by the data layer which has access to <c>TracksDbContext</c>.
/// </summary>
public interface ICardAttachmentCleanupService
{
    /// <summary>
    /// Clears <c>FileNodeId</c> on all card attachments that reference the given file.
    /// The attachment records are preserved (not deleted) so the UI can show deletion history.
    /// </summary>
    /// <param name="fileNodeId">The Files module file node ID that was deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of attachments that were updated.</returns>
    Task<int> ClearFileReferencesAsync(Guid fileNodeId, CancellationToken cancellationToken = default);
}
