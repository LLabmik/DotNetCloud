namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Background service that detects new device photos and videos and uploads them to the
/// user's DotNetCloud storage using the chunked upload protocol.
/// </summary>
public interface IMediaAutoUploadService
{
    /// <summary>Starts the periodic background scan. Safe to call multiple times.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the background scan and waits for any in-progress upload to finish.</summary>
    Task StopAsync();

    /// <summary>Triggers an immediate scan and upload, bypassing the normal timer.</summary>
    Task ScanAndUploadNowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves (find-or-create) the target AutoUpload folder chain for the given timestamp.
    /// Returns the month-level folder ID (e.g. <c>AutoUpload/2026/07</c>).
    /// Callers can pass this as <c>parentId</c> to <c>IFileRestClient.UploadFileAsync</c>.
    /// </summary>
    /// <param name="serverBaseUrl">The server base URL.</param>
    /// <param name="accessToken">The OAuth2 access token.</param>
    /// <param name="timestamp">The media timestamp (defaults to <see cref="DateTime.UtcNow"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved month-level folder ID, or null if folder creation fails.</returns>
    Task<Guid?> ResolveUploadTargetFolderAsync(
        string serverBaseUrl, string accessToken,
        DateTime? timestamp = null, CancellationToken ct = default);

    /// <summary>Whether the background watcher is currently active.</summary>
    bool IsRunning { get; }
}
