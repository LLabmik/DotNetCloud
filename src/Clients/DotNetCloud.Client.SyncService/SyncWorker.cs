using DotNetCloud.Client.SyncService.ContextManager;
using DotNetCloud.Client.SyncService.Ipc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncService;

/// <summary>
/// Main background service that orchestrates sync-context lifecycle and IPC communication.
/// Starts the context manager (loads persisted accounts) and the IPC server,
/// then runs until the host requests shutdown.
/// </summary>
public sealed class SyncWorker : BackgroundService
{
    private readonly ISyncContextManager _contextManager;
    private readonly IIpcServer _ipcServer;
    private readonly ILogger<SyncWorker> _logger;

    /// <summary>Initializes a new <see cref="SyncWorker"/>.</summary>
    public SyncWorker(
        ISyncContextManager contextManager,
        IIpcServer ipcServer,
        ILogger<SyncWorker> logger)
    {
        _contextManager = contextManager;
        _ipcServer = ipcServer;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DotNetCloud Sync Service starting.");

        _contextManager.SyncProgress += OnSyncProgress;

        // Load persisted contexts before accepting IPC connections
        await _contextManager.LoadContextsAsync(stoppingToken);

        var loaded = await _contextManager.GetContextsAsync();
        _logger.LogInformation(
            "DotNetCloud Sync Service running — {Count} context(s) active.", loaded.Count);

        await _ipcServer.StartAsync(stoppingToken);

        // Hold until the host signals shutdown
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown
        }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DotNetCloud Sync Service stopping.");

        _contextManager.SyncProgress -= OnSyncProgress;

        await _ipcServer.StopAsync(cancellationToken);
        await _contextManager.StopAllAsync(cancellationToken);

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("DotNetCloud Sync Service stopped.");
    }

    private void OnSyncProgress(object? sender, SyncProgressEventArgs args)
    {
        _logger.LogInformation(
            "Sync progress update: ContextId={ContextId}, State={State}, PendingUploads={PendingUploads}, PendingDownloads={PendingDownloads}.",
            args.ContextId,
            args.Status.State,
            args.Status.PendingUploads,
            args.Status.PendingDownloads);
    }
}
