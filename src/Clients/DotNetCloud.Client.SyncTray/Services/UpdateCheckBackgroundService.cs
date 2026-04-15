using DotNetCloud.Client.Core.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.Services;

/// <summary>
/// Background service that periodically checks for available updates
/// and raises events when a newer version is discovered.
/// </summary>
public sealed class UpdateCheckBackgroundService : IDisposable
{
    private readonly IClientUpdateService _updateService;
    private readonly ILogger<UpdateCheckBackgroundService> _logger;
    private Timer? _timer;
    private bool _disposed;

    /// <summary>Default interval between update checks (24 hours).</summary>
    public static readonly TimeSpan DefaultCheckInterval = TimeSpan.FromHours(24);

    /// <summary>Gets or sets the interval between periodic update checks.</summary>
    public TimeSpan CheckInterval { get; set; } = DefaultCheckInterval;

    /// <summary>Gets or sets whether automatic background checking is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets the result of the most recent update check, if any.</summary>
    public UpdateCheckResult? LatestCheckResult { get; private set; }

    /// <summary>Gets the UTC time of the last successful check.</summary>
    public DateTime? LastCheckedAtUtc { get; private set; }

    /// <summary>
    /// Raised when a background check discovers a newer version.
    /// </summary>
    public event EventHandler<UpdateCheckResult>? UpdateAvailable;

    /// <summary>Initializes a new <see cref="UpdateCheckBackgroundService"/>.</summary>
    public UpdateCheckBackgroundService(
        IClientUpdateService updateService,
        ILogger<UpdateCheckBackgroundService> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    /// <summary>
    /// Starts the periodic update-check timer. Performs an initial check after
    /// a short delay to avoid slowing down application startup.
    /// </summary>
    public void Start()
    {
        if (_disposed) return;

        // Initial check after 30 seconds, then at the configured interval.
        _timer = new Timer(
            _ => _ = CheckAsync(),
            state: null,
            dueTime: TimeSpan.FromSeconds(30),
            period: CheckInterval);

        _logger.LogInformation("Update check background service started (interval: {Interval}).", CheckInterval);
    }

    /// <summary>Stops the periodic timer without disposing the service.</summary>
    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.LogInformation("Update check background service stopped.");
    }

    /// <summary>
    /// Performs a single on-demand update check.
    /// Can be called independently of the periodic timer.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("Checking for updates...");
            var result = await _updateService.CheckForUpdateAsync(ct);
            LatestCheckResult = result;
            LastCheckedAtUtc = DateTime.UtcNow;

            if (result.IsUpdateAvailable)
            {
                _logger.LogInformation(
                    "Update available: {Current} → {Latest}.",
                    result.CurrentVersion, result.LatestVersion);
                UpdateAvailable?.Invoke(this, result);
            }
            else
            {
                _logger.LogDebug("No update available (current: {Version}).", result.CurrentVersion);
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Background update check failed.");
            return LatestCheckResult ?? new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = "unknown",
                LatestVersion = "unknown",
            };
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
    }
}
