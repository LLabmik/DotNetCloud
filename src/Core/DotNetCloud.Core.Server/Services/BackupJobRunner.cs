using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Runs a backup in the background so an HTTP request never has to stay open for the whole
/// operation. The Admin UI starts a job, then polls <see cref="GetStatus"/> until it completes.
/// </summary>
/// <remarks>
/// This is what makes large backups possible from the web UI: the browser's default
/// <see cref="HttpClient"/> timeout is 100 seconds, which would otherwise abort (and therefore
/// cancel) any backup larger than a few gigabytes.
/// </remarks>
public sealed class BackupJobRunner : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupJobRunner> _logger;

    private readonly object _gate = new();
    private BackupStatusInfo _status = new();
    private CancellationTokenSource? _cancellation;
    private Task? _job;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupJobRunner"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory used to resolve the scoped backup service.</param>
    /// <param name="logger">Logger.</param>
    public BackupJobRunner(IServiceScopeFactory scopeFactory, ILogger<BackupJobRunner> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Returns a snapshot of the current job state.</summary>
    public BackupStatusInfo GetStatus()
    {
        lock (_gate)
        {
            return new BackupStatusInfo
            {
                IsRunning = _status.IsRunning,
                StartedAt = _status.StartedAt,
                CompletedAt = _status.CompletedAt,
                Phase = _status.Phase,
                FilesProcessed = _status.FilesProcessed,
                FilesTotal = _status.FilesTotal,
                BytesProcessed = _status.BytesProcessed,
                BytesTotal = _status.BytesTotal,
                CancelRequested = _status.CancelRequested,
                LastError = _status.LastError,
                LastBackupTime = _status.LastBackupTime,
                LastBackupSuccess = _status.LastBackupSuccess,
                LastBackupPath = _status.LastBackupPath,
                LastBackupSizeBytes = _status.LastBackupSizeBytes,
                LastBackupFileCount = _status.LastBackupFileCount,
            };
        }
    }

    /// <summary>
    /// Starts a background backup. Returns <see langword="false"/> when one is already running.
    /// </summary>
    /// <param name="options">Backup options (data directory, provider, compression, …).</param>
    /// <param name="outputPath">Optional explicit output path.</param>
    /// <param name="error">Reason the job could not start.</param>
    public bool TryStart(BackupOptions options, string? outputPath, out string? error)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_gate)
        {
            if (_status.IsRunning)
            {
                error = "A backup is already running.";
                return false;
            }

            _cancellation = new CancellationTokenSource();
            _status = new BackupStatusInfo
            {
                IsRunning = true,
                StartedAt = DateTime.UtcNow,
                Phase = "starting",
            };

            var cancellationToken = _cancellation.Token;
            _job = Task.Run(() => RunAsync(options, outputPath, cancellationToken));
            error = null;
            return true;
        }
    }

    /// <summary>Requests cancellation of the running backup.</summary>
    /// <returns><see langword="false"/> when no backup is running.</returns>
    public bool TryCancel()
    {
        lock (_gate)
        {
            if (!_status.IsRunning || _cancellation is null)
                return false;

            _status.CancelRequested = true;
            _status.Phase = "cancelling";

            try
            { _cancellation.Cancel(); }
            catch (ObjectDisposedException) { /* job finished between the checks */ }

            return true;
        }
    }

    private async Task RunAsync(BackupOptions options, string? outputPath, CancellationToken cancellationToken)
    {
        // The runner is a singleton; the backup service is scoped.
        using var scope = _scopeFactory.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

        options.Progress = progress =>
        {
            lock (_gate)
            {
                _status.Phase = progress.Phase;
                _status.FilesProcessed = progress.FilesProcessed;
                _status.FilesTotal = progress.FilesTotal;
                _status.BytesProcessed = progress.BytesProcessed;
                _status.BytesTotal = progress.BytesTotal;
            }
        };

        try
        {
            var result = await backupService.CreateBackupAsync(outputPath, options, cancellationToken);

            lock (_gate)
            {
                _status.IsRunning = false;
                _status.CompletedAt = DateTime.UtcNow;
                _status.Phase = result.Success ? "completed" : "failed";
                _status.LastError = result.Success ? null : result.ErrorMessage;
                _status.LastBackupTime = DateTime.UtcNow;
                _status.LastBackupSuccess = result.Success;
                _status.LastBackupPath = result.FilePath;
                _status.LastBackupSizeBytes = result.SizeBytes;
                _status.LastBackupFileCount = result.FileCount;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background backup was cancelled.");
            lock (_gate)
            {
                _status.IsRunning = false;
                _status.CompletedAt = DateTime.UtcNow;
                _status.Phase = "cancelled";
                _status.LastError = "Backup cancelled.";
                _status.LastBackupTime = DateTime.UtcNow;
                _status.LastBackupSuccess = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background backup failed.");
            lock (_gate)
            {
                _status.IsRunning = false;
                _status.CompletedAt = DateTime.UtcNow;
                _status.Phase = "failed";
                _status.LastError = ex.Message;
                _status.LastBackupTime = DateTime.UtcNow;
                _status.LastBackupSuccess = false;
            }
        }
        finally
        {
            lock (_gate)
            {
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            try
            { _cancellation?.Cancel(); }
            catch (ObjectDisposedException) { /* already disposed */ }

            _cancellation?.Dispose();
            _cancellation = null;
        }
    }
}
