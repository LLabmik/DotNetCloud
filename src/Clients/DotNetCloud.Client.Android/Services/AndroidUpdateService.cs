using DotNetCloud.Client.Core.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Android update service that checks for updates once per day on app launch,
/// respects "don't remind me for this version" preference, and directs users
/// to the appropriate store listing.
/// </summary>
public sealed class AndroidUpdateService : IAndroidUpdateService
{
    internal const string PrefLastCheckDate = "update_last_check_date";
    internal const string PrefDismissedVersion = "update_dismissed_version";

    private readonly IClientUpdateService _updateService;
    private readonly IAppPreferences _preferences;
    private readonly ILogger<AndroidUpdateService> _logger;

    /// <summary>Initializes a new <see cref="AndroidUpdateService"/>.</summary>
    public AndroidUpdateService(
        IClientUpdateService updateService,
        IAppPreferences preferences,
        ILogger<AndroidUpdateService> logger)
    {
        _updateService = updateService;
        _preferences = preferences;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult?> CheckOnLaunchAsync(CancellationToken ct = default)
    {
        // Rate-limit: once per day.
        var lastCheck = _preferences.Get(PrefLastCheckDate, string.Empty);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        if (string.Equals(lastCheck, today, StringComparison.Ordinal))
        {
            _logger.LogDebug("Update check already performed today; skipping.");
            return null;
        }

        UpdateCheckResult result;
        try
        {
            result = await _updateService.CheckForUpdateAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Update check failed.");
            return null;
        }

        // Record that we checked today regardless of outcome.
        _preferences.Set(PrefLastCheckDate, today);

        if (!result.IsUpdateAvailable)
        {
            _logger.LogDebug("No update available (current: {Current}, latest: {Latest}).",
                result.CurrentVersion, result.LatestVersion);
            return null;
        }

        // Respect "don't remind me for this version".
        var dismissed = _preferences.Get(PrefDismissedVersion, string.Empty);
        if (string.Equals(dismissed, result.LatestVersion, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Version {Version} was dismissed by the user.", result.LatestVersion);
            return null;
        }

        _logger.LogInformation("Update available: {Current} → {Latest}.",
            result.CurrentVersion, result.LatestVersion);
        return result;
    }

    /// <inheritdoc/>
    public void DismissVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        _preferences.Set(PrefDismissedVersion, version);
        _logger.LogInformation("User dismissed update notification for version {Version}.", version);
    }

    /// <inheritdoc/>
    public async Task OpenStoreListingAsync(string? releaseUrl = null)
    {
        var storeUrl = GetStoreUrl();
        var url = storeUrl ?? releaseUrl;

        if (string.IsNullOrEmpty(url))
        {
            _logger.LogWarning("No store or release URL available to open.");
            return;
        }

        try
        {
            await Microsoft.Maui.ApplicationModel.Browser.Default.OpenAsync(
                new Uri(url),
                new Microsoft.Maui.ApplicationModel.BrowserLaunchOptions
                {
                    LaunchMode = Microsoft.Maui.ApplicationModel.BrowserLaunchMode.External,
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open store listing at {Url}.", url);
        }
    }

    private static string? GetStoreUrl()
    {
#if GOOGLEPLAY
        return "https://play.google.com/store/apps/details?id=net.dotnetcloud.client";
#elif FDROID
        return "https://f-droid.org/packages/net.dotnetcloud.client";
#else
        return null;
#endif
    }
}
