using System.Diagnostics;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Convenience helpers for reporting background service cycles to
/// <see cref="IBackgroundServiceTracker"/>.
/// </summary>
public static class BackgroundServiceTrackerExtensions
{
    /// <summary>
    /// Runs one tracked cycle: records the start, invokes <paramref name="cycle"/>,
    /// and always records completion (success or failure) in a <c>finally</c>.
    /// </summary>
    /// <param name="tracker">The tracker; <c>null</c> disables tracking.</param>
    /// <param name="serviceName">Human-readable service name.</param>
    /// <param name="cycle">The cycle to execute.</param>
    /// <returns>A task that completes when the cycle finishes.</returns>
    public static async Task RunTrackedAsync(
        this IBackgroundServiceTracker? tracker,
        string serviceName,
        Func<Task> cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        tracker?.RecordRunStarting(serviceName);
        var startedAt = Stopwatch.GetTimestamp();
        var success = true;
        string? message = null;

        try
        {
            await cycle().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            success = false;
            message = ex.Message;
            throw;
        }
        finally
        {
            tracker?.RecordRun(
                serviceName,
                DateTimeOffset.UtcNow,
                Stopwatch.GetElapsedTime(startedAt),
                success,
                message);
        }
    }
}
