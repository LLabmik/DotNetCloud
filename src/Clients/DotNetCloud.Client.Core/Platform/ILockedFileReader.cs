namespace DotNetCloud.Client.Core.Platform;

/// <summary>
/// Provides a last-resort mechanism for reading files that are locked by other processes,
/// using platform-specific techniques such as Windows Volume Shadow Copy (VSS).
/// </summary>
public interface ILockedFileReader : IDisposable
{
    /// <summary>
    /// Attempts to open a readable stream for a file that could not be accessed via normal I/O.
    /// Returns <see langword="null"/> when the implementation cannot read the file
    /// (e.g. because VSS snapshot creation failed, or the platform is not Windows).
    /// </summary>
    /// <param name="path">Full path to the file to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Stream?> TryReadLockedFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases any platform resources (e.g. Windows VSS shadow copy) created during
    /// the current sync pass. Should be called in a <c>finally</c> block after each
    /// sync pass completes.
    /// </summary>
    void ReleaseSnapshot();
}
