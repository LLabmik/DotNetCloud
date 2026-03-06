using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Core WOPI operations for Collabora Online integration.
/// Handles CheckFileInfo, GetFile, and PutFile with permission enforcement.
/// </summary>
public interface IWopiService
{
    /// <summary>
    /// Returns file metadata for WOPI CheckFileInfo.
    /// Validates the caller's access permissions on the file.
    /// </summary>
    /// <param name="fileId">The file node ID.</param>
    /// <param name="caller">The caller context (from validated WOPI token).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>WOPI CheckFileInfo response, or null if file not found.</returns>
    Task<WopiCheckFileInfoResponse?> CheckFileInfoAsync(Guid fileId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the file content stream for WOPI GetFile.
    /// Reads the current version from the storage engine via the download service.
    /// </summary>
    /// <param name="fileId">The file node ID.</param>
    /// <param name="caller">The caller context (from validated WOPI token).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file content stream and MIME type, or null if file not found.</returns>
    Task<(Stream Content, string MimeType, string FileName)?> GetFileAsync(Guid fileId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves edited file content from Collabora via WOPI PutFile.
    /// Creates a new file version using the chunked upload pipeline.
    /// </summary>
    /// <param name="fileId">The file node ID.</param>
    /// <param name="content">The file content stream from Collabora.</param>
    /// <param name="caller">The caller context (from validated WOPI token).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated file timestamp in ISO-8601 format.</returns>
    Task<string> PutFileAsync(Guid fileId, Stream content, CallerContext caller, CancellationToken cancellationToken = default);
}
