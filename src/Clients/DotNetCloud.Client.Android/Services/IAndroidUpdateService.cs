using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Android-specific update notification service.
/// Checks for updates on app launch and provides dismissable version-specific notifications.
/// </summary>
public interface IAndroidUpdateService
{
    /// <summary>
    /// Checks for available updates, respecting the once-per-day and dismissed-version preferences.
    /// Returns <c>null</c> if no actionable update is available or the check was suppressed.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="UpdateCheckResult"/> if notification should be shown; otherwise <c>null</c>.</returns>
    Task<UpdateCheckResult?> CheckOnLaunchAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks the specified version as dismissed so the user is not reminded again.
    /// </summary>
    /// <param name="version">The version string to dismiss.</param>
    void DismissVersion(string version);

    /// <summary>
    /// Opens the appropriate store listing (Google Play or F-Droid) or the release page.
    /// </summary>
    /// <param name="releaseUrl">Fallback URL if no store link is available.</param>
    Task OpenStoreListingAsync(string? releaseUrl = null);
}
