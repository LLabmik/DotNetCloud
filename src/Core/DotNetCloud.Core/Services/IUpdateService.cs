using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Provides update checking against the GitHub Releases API.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks whether a newer version is available.
    /// </summary>
    /// <param name="currentVersion">
    /// The version to compare against. When <see langword="null"/>, the running server version is used.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="UpdateCheckResult"/> describing availability.</returns>
    Task<UpdateCheckResult> CheckForUpdateAsync(string? currentVersion = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the latest published release, or <see langword="null"/> if unavailable.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent releases.
    /// </summary>
    /// <param name="count">Maximum number of releases to return (default 5).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ReleaseInfo>> GetRecentReleasesAsync(int count = 5, CancellationToken ct = default);
}
