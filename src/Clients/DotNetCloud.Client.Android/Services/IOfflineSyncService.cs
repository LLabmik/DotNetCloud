namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Orchestrates delivery of operations queued while the device was offline.
/// Flushes the queue in priority order when connectivity is restored.
/// </summary>
public interface IOfflineSyncService
{
    /// <summary>
    /// Starts connectivity monitoring and performs an initial flush if the device is online.
    /// Safe to call multiple times.
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Replays all queued operations to the server in priority order, removing those that
    /// are delivered successfully. Stops at the first failure to preserve ordering.
    /// </summary>
    /// <returns>The number of operations successfully flushed.</returns>
    Task<int> FlushAllAsync(CancellationToken ct = default);

    /// <summary>Returns true when at least one operation is queued awaiting delivery.</summary>
    Task<bool> HasPendingAsync(CancellationToken ct = default);
}
