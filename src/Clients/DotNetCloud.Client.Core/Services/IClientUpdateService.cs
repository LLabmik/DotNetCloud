using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Core.Services;

/// <summary>
/// Client-side update checking, downloading, and applying service.
/// Communicates with the server's update API and optionally falls back to GitHub directly.
/// </summary>
public interface IClientUpdateService
{
    /// <summary>
    /// Checks the server (or GitHub fallback) for available updates.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="UpdateCheckResult"/> describing availability.</returns>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the specified release asset to a temporary directory with optional progress reporting.
    /// </summary>
    /// <param name="asset">The asset to download.</param>
    /// <param name="progress">Optional progress reporter (0.0 – 1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full path to the downloaded file.</returns>
    Task<string> DownloadUpdateAsync(ReleaseAsset asset, IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Applies a previously downloaded update from the given path.
    /// </summary>
    /// <param name="downloadPath">Path to the downloaded update archive.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ApplyUpdateAsync(string downloadPath, CancellationToken ct = default);

    /// <summary>
    /// Raised when a background check discovers that an update is available.
    /// </summary>
    event EventHandler<UpdateCheckResult>? UpdateAvailable;
}
