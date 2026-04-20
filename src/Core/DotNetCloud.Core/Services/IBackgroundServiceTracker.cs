using System.Collections.Concurrent;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Tracks last-run information for periodic background services.
/// Register as a singleton so all services report to the same instance.
/// </summary>
public interface IBackgroundServiceTracker
{
    /// <summary>
    /// Records the completion of a background service cycle.
    /// </summary>
    /// <param name="serviceName">Human-readable service name.</param>
    /// <param name="completedAt">When the cycle completed.</param>
    /// <param name="duration">How long the cycle took.</param>
    /// <param name="success">Whether the cycle completed without error.</param>
    /// <param name="message">Optional result message (e.g. "Deleted 3 items").</param>
    void RecordRun(string serviceName, DateTimeOffset completedAt, TimeSpan duration, bool success, string? message = null);

    /// <summary>
    /// Gets the current status snapshot for all tracked services.
    /// </summary>
    IReadOnlyDictionary<string, BackgroundServiceStatus> GetAll();
}

/// <summary>
/// Status snapshot for a single background service.
/// </summary>
public sealed class BackgroundServiceStatus
{
    /// <summary>Human-readable service name.</summary>
    public required string ServiceName { get; init; }

    /// <summary>When the last cycle completed.</summary>
    public DateTimeOffset LastRunAt { get; set; }

    /// <summary>Duration of the last cycle.</summary>
    public TimeSpan LastRunDuration { get; set; }

    /// <summary>Whether the last cycle succeeded.</summary>
    public bool LastRunSuccess { get; set; }

    /// <summary>Optional result message from the last run.</summary>
    public string? LastRunMessage { get; set; }

    /// <summary>Total number of successful cycles since startup.</summary>
    public int TotalRuns { get; set; }

    /// <summary>Total number of failed cycles since startup.</summary>
    public int TotalFailures { get; set; }
}

/// <summary>
/// Default thread-safe in-memory implementation of <see cref="IBackgroundServiceTracker"/>.
/// </summary>
public sealed class BackgroundServiceTracker : IBackgroundServiceTracker
{
    private readonly ConcurrentDictionary<string, BackgroundServiceStatus> _services = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void RecordRun(string serviceName, DateTimeOffset completedAt, TimeSpan duration, bool success, string? message = null)
    {
        _services.AddOrUpdate(
            serviceName,
            _ => new BackgroundServiceStatus
            {
                ServiceName = serviceName,
                LastRunAt = completedAt,
                LastRunDuration = duration,
                LastRunSuccess = success,
                LastRunMessage = message,
                TotalRuns = success ? 1 : 0,
                TotalFailures = success ? 0 : 1,
            },
            (_, existing) =>
            {
                existing.LastRunAt = completedAt;
                existing.LastRunDuration = duration;
                existing.LastRunSuccess = success;
                existing.LastRunMessage = message;
                if (success)
                    existing.TotalRuns++;
                else
                    existing.TotalFailures++;
                return existing;
            });
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, BackgroundServiceStatus> GetAll() => _services;
}
