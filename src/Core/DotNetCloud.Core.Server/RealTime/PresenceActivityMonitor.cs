using DotNetCloud.Core.Constants;
using DotNetCloud.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Periodically reconciles user presence so the green → yellow (idle) transition happens
/// automatically ~N minutes after a user's last real interaction — no per-connection
/// heartbeat required. On each sweep it reads the admin idle threshold
/// (<c>PresenceIdleTimeoutMinutes</c>, default 3, clamped 1–60) at runtime, pushes it into
/// <see cref="PresenceService"/>, and recomputes the display state of every online user.
/// </summary>
/// <remarks>
/// The sweep interval (30 s) bounds how quickly admin threshold changes and Away/Online
/// transitions propagate (≤30 s). DND toggles and activity reports broadcast immediately
/// via <see cref="PresenceService.PresenceStateChanged"/> and do not wait for the sweep.
/// </remarks>
internal sealed class PresenceActivityMonitor : BackgroundService
{
    /// <summary>
    /// How often the monitor re-reads the admin threshold and sweeps online users.
    /// </summary>
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    private readonly PresenceService _presenceService;
    private readonly UserConnectionTracker _connectionTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PresenceActivityMonitor> _logger;

    public PresenceActivityMonitor(
        PresenceService presenceService,
        UserConnectionTracker connectionTracker,
        IServiceScopeFactory scopeFactory,
        ILogger<PresenceActivityMonitor> logger)
    {
        _presenceService = presenceService;
        _connectionTracker = connectionTracker;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Presence activity monitor started (sweep interval {Interval}s).", SweepInterval.TotalSeconds);

        // Short warm-up so DB initialisation/settings seeding completes before the first read.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(SweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Presence activity sweep failed; will retry next interval.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        _logger.LogInformation("Presence activity monitor stopped.");
    }

    /// <summary>
    /// Runs a single presence reconciliation pass: reads the admin idle threshold at runtime,
    /// applies it to <see cref="PresenceService"/>, and recomputes every online user's state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        var idle = await ReadIdleThresholdAsync(cancellationToken).ConfigureAwait(false);
        await SweepAsync(idle, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies a resolved idle threshold and recomputes every online user's display state.
    /// Exposed for tests to drive a sweep with a fixed threshold (no DB).
    /// </summary>
    /// <param name="idleThreshold">The idle threshold to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task SweepAsync(TimeSpan idleThreshold, CancellationToken cancellationToken)
    {
        _presenceService.UpdateIdleThreshold(idleThreshold);

        var onlineUsers = _connectionTracker.GetOnlineUsers();
        foreach (var userId in onlineUsers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _presenceService.RecomputeAsync(userId).ConfigureAwait(false);
        }

        _logger.LogDebug(
            "Presence sweep applied idle threshold {Idle} across {Count} online user(s).",
            idleThreshold, onlineUsers.Count);
    }

    private async Task<TimeSpan> ReadIdleThresholdAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IAdminSettingsService>();

        var setting = await settings
            .GetSettingAsync(SystemSettingKeys.CoreModule, SystemSettingKeys.PresenceIdleTimeoutMinutes)
            .ConfigureAwait(false);

        var idle = PresenceIdleTimeout.Resolve(setting?.Value);
        if (idle != PresenceService.DefaultIdleThreshold)
        {
            _logger.LogDebug("Presence idle threshold is {Idle} (admin setting).", idle);
        }

        return idle;
    }
}
