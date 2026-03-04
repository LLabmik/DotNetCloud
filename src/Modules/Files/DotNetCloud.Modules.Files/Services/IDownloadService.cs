using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Reconstructs file content from stored chunks for download.
/// </summary>
public interface IDownloadService
{
    /// <summary>Opens a stream that reconstructs a file's current version from chunks.</summary>
    Task<Stream> DownloadCurrentAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Opens a stream that reconstructs a specific file version from chunks.</summary>
    Task<Stream> DownloadVersionAsync(Guid fileVersionId, CallerContext caller, CancellationToken cancellationToken = default);
}
