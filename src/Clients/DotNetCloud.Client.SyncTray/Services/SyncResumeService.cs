using System.Runtime.InteropServices;
using DotNetCloud.Client.Core.Sync;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.Services;

/// <summary>
/// Detects when the machine wakes from sleep/suspend and re-synchronizes all
/// sync contexts so the tray client catches up on changes that happened while
/// the machine was asleep.
///
/// Detection strategy (cross-platform — Windows and Linux):
///  - A clock-jump detector runs on every platform. If far more wall-clock time
///    passes than the monitor interval, the machine was suspended (timers do not
///    run while asleep, so the wall clock jumps ahead on resume).
///  - On Linux, a SIGCONT handler fires immediately when the process is continued
///    after being stopped, giving an instant wake signal in addition to the
///    polling fallback.
///
/// On resume, each sync context is told to run an immediate catch-up pass so
/// files changed on the server while the machine slept are pulled down.
/// </summary>
public sealed class SyncResumeService : IDisposable
{
    /// <summary>How often the clock-jump detector ticks while the machine is awake.</summary>
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(30);

    /// <summary>How long to wait after wake before re-syncing so the network stack can come back.</summary>
    private static readonly TimeSpan ResumeWaitForNetwork = TimeSpan.FromSeconds(5);

    private readonly ISyncContextManager _syncManager;
    private readonly ILogger<SyncResumeService> _logger;

    private Timer? _timer;
    private PosixSignalRegistration? _sigcontRegistration;
    private DateTime _lastTickUtc = DateTime.UtcNow;
    private int _resumeRunning;
    private bool _disposed;

    /// <summary>Initializes a new <see cref="SyncResumeService"/>.</summary>
    public SyncResumeService(ISyncContextManager syncManager, ILogger<SyncResumeService> logger)
    {
        _syncManager = syncManager;
        _logger = logger;
    }

    /// <summary>
    /// Starts the sleep/resume monitor. Registers the platform wake signal
    /// (Linux SIGCONT) and begins the periodic clock-jump detector.
    /// </summary>
    public void Start()
    {
        if (_disposed)
            return;

        _lastTickUtc = DateTime.UtcNow;

        // Linux: immediate wake signal when the process is continued after suspend.
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                _sigcontRegistration = PosixSignalRegistration.Create(PosixSignal.SIGCONT, ctx =>
                {
                    // Keep the process alive — we handle resume ourselves.
                    ctx.Cancel = true;
                    _logger.LogInformation("SIGCONT received — machine resumed from sleep.");
                    _ = Task.Run(() => OnResumedAsync());
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register SIGCONT handler for sleep/resume detection");
            }
        }

        _timer = new Timer(
            _ => _ = CheckForSleepAsync(),
            state: null,
            dueTime: MonitorInterval,
            period: MonitorInterval);

        _logger.LogInformation("Sync resume monitor started (interval: {Interval}).", MonitorInterval);
    }

    private async Task CheckForSleepAsync()
    {
        if (_disposed)
            return;

        var now = DateTime.UtcNow;
        var elapsed = now - _lastTickUtc;
        _lastTickUtc = now;

        // If far more wall-clock time passed than the monitor interval, the
        // machine was suspended (timers don't run while asleep, so the wall
        // clock jumps ahead on resume).
        if (elapsed > MonitorInterval * 2)
        {
            _logger.LogInformation("Sleep/resume detected via clock jump ({Elapsed} elapsed)", elapsed);
            await OnResumedAsync();
        }
    }

    private async Task OnResumedAsync()
    {
        // Guard against overlapping resume passes (SIGCONT + clock-jump can race).
        if (Interlocked.CompareExchange(ref _resumeRunning, 1, 0) != 0)
            return;

        try
        {
            // Give the network stack a moment to come back after wake.
            try
            {
                await Task.Delay(ResumeWaitForNetwork);
            }
            catch
            {
                return;
            }

            var contexts = await _syncManager.GetContextsAsync();
            _logger.LogInformation("Wake from sleep — re-syncing {Count} context(s)", contexts.Count);

            foreach (var context in contexts)
            {
                try
                {
                    await _syncManager.SyncNowAsync(context.Id);
                    _logger.LogDebug("Wake re-sync triggered for context {ContextId} ({DisplayName})", context.Id, context.DisplayName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to re-sync context {ContextId} after wake", context.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wake-from-sleep re-sync failed");
        }
        finally
        {
            Interlocked.Exchange(ref _resumeRunning, 0);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer?.Dispose();
        _timer = null;
        _sigcontRegistration?.Dispose();
        _sigcontRegistration = null;
    }
}
