namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Background service that detects new device photos and videos and uploads them to the
/// user's DotNetCloud storage using the chunked upload protocol.
/// </summary>
public interface IMediaAutoUploadService
{
    /// <summary>Starts the periodic background scan. Safe to call multiple times.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the background scan and waits for any in-progress upload to finish.</summary>
    Task StopAsync();

    /// <summary>Triggers an immediate scan and upload, bypassing the normal timer.</summary>
    Task ScanAndUploadNowAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the background watcher is currently active.</summary>
    bool IsRunning { get; }
}
