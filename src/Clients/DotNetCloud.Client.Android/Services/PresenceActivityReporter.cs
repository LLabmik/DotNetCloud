using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Throttled activity reporter that forwards genuine user interaction to the CoreHub
/// <c>PingAsync</c> presence-activity endpoint. Interaction sources call
/// <see cref="IActivityReporter.NotifyInteraction"/> (platform touch/key events, page
/// navigation, message sends); this class coalesces them to at most one server report per
/// <see cref="MinReportInterval"/> so we don't spam the hub.
/// </summary>
public sealed class PresenceActivityReporter : IActivityReporter
{
    /// <summary>
    /// Minimum time between server activity reports.
    /// </summary>
    internal static readonly TimeSpan MinReportInterval = TimeSpan.FromSeconds(20);

    private readonly ICoreHubClient _hub;
    private readonly ILogger<PresenceActivityReporter> _logger;
    private readonly object _gate = new();
    private DateTime _lastReportUtc = DateTime.MinValue;

    /// <summary>Initializes a new <see cref="PresenceActivityReporter"/>.</summary>
    public PresenceActivityReporter(
        ICoreHubClient hub,
        ILogger<PresenceActivityReporter> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public void NotifyInteraction()
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            if (now - _lastReportUtc < MinReportInterval)
            {
                return;
            }

            _lastReportUtc = now;
        }

        // Fire-and-forget: a transient hub hiccup must never surface to the caller.
        _ = Task.Run(async () =>
        {
            try
            {
                await _hub.ReportActivityAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Presence activity report failed (hub likely disconnected).");
            }
        });
    }
}
