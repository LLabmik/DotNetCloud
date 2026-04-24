using System.Diagnostics;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that processes scheduled admin shared-folder rescans and manual reindex requests.
/// </summary>
internal sealed class AdminSharedFolderMaintenanceService : BackgroundService, IAdminSharedFolderMaintenanceScheduler
{
    private const string ServiceName = "Admin Shared Folder Maintenance";
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminSharedFolderMaintenanceService> _logger;
    private readonly IBackgroundServiceTracker _tracker;
    private readonly SemaphoreSlim _triggerSemaphore = new(0, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSharedFolderMaintenanceService"/> class.
    /// </summary>
    public AdminSharedFolderMaintenanceService(
        IServiceScopeFactory scopeFactory,
        ILogger<AdminSharedFolderMaintenanceService> logger,
        IBackgroundServiceTracker tracker)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _tracker = tracker;
    }

    /// <inheritdoc />
    public void TriggerProcessing()
    {
        try
        {
            _triggerSemaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // A maintenance cycle is already pending.
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCycleAsync("initial", stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var triggered = await WaitForNextRunAsync(stoppingToken);
            await RunCycleAsync(triggered ? "manual" : "scheduled", stoppingToken);
        }
    }

    internal async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var reindexDispatcher = scope.ServiceProvider.GetService<IAdminSharedFolderReindexDispatcher>();
        var now = DateTime.UtcNow;

        var dueFolders = await db.AdminSharedFolders
            .Where(folder => folder.ReindexState == AdminSharedFolderReindexState.Requested
                || (folder.NextScheduledScanAt.HasValue && folder.NextScheduledScanAt.Value <= now))
            .OrderBy(folder => folder.NextScheduledScanAt ?? DateTime.MaxValue)
            .ThenBy(folder => folder.DisplayName)
            .ToListAsync(cancellationToken);

        if (dueFolders.Count == 0)
        {
            return;
        }

        var reindexCandidates = new List<AdminSharedFolderDefinition>();
        foreach (var folder in dueFolders.Where(folder => folder.ReindexState == AdminSharedFolderReindexState.Requested))
        {
            folder.ReindexState = AdminSharedFolderReindexState.Running;
            folder.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var folder in dueFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var processedAt = DateTime.UtcNow;
            var scanSucceeded = TryProbeSourcePath(folder.SourcePath, out var failureMessage);
            var wasReindexRequested = folder.ReindexState == AdminSharedFolderReindexState.Running;

            if (!scanSucceeded)
            {
                _logger.LogWarning(
                    "Admin shared folder scan failed for {SharedFolderId} at {SourcePath}: {Failure}",
                    folder.Id,
                    folder.SourcePath,
                    failureMessage);
            }

            folder.LastScanStatus = scanSucceeded
                ? AdminSharedFolderScanStatus.Succeeded
                : AdminSharedFolderScanStatus.Failed;
            folder.NextScheduledScanAt = ResolveNextScheduledScanAt(folder.CrawlMode, processedAt);
            folder.UpdatedAt = processedAt;

            if (wasReindexRequested)
            {
                if (scanSucceeded)
                {
                    reindexCandidates.Add(folder);
                }
                else
                {
                    folder.ReindexState = AdminSharedFolderReindexState.Idle;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (reindexCandidates.Count == 0)
        {
            return;
        }

        var reindexAccepted = reindexDispatcher is not null
            && await reindexDispatcher.RequestFilesReindexAsync(cancellationToken);
        var completedAt = DateTime.UtcNow;

        foreach (var folder in reindexCandidates)
        {
            folder.ReindexState = AdminSharedFolderReindexState.Idle;
            folder.UpdatedAt = completedAt;

            if (reindexAccepted)
            {
                folder.LastIndexedAt = completedAt;
            }
            else
            {
                folder.LastScanStatus = AdminSharedFolderScanStatus.Failed;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (reindexAccepted)
        {
            _logger.LogInformation(
                "Queued Files-module reindex for {Count} admin shared folders",
                reindexCandidates.Count);
        }
        else
        {
            _logger.LogWarning(
                "Files-module reindex request for {Count} admin shared folders was not accepted",
                reindexCandidates.Count);
        }
    }

    private async Task RunCycleAsync(string trigger, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("{Service} cycle starting ({Trigger})", ServiceName, trigger);
            await ProcessPendingAsync(cancellationToken);
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: true);
            _logger.LogInformation("{Service} cycle completed in {Elapsed:F1}s", ServiceName, sw.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: false, message: ex.Message);
            _logger.LogError(ex, "Error during {Service}", ServiceName);
        }
    }

    private async Task<bool> WaitForNextRunAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(Interval, linkedCts.Token);
        var triggerTask = _triggerSemaphore.WaitAsync(linkedCts.Token);

        var completed = await Task.WhenAny(delayTask, triggerTask);
        if (completed == triggerTask)
        {
            await linkedCts.CancelAsync();
            return true;
        }

        return false;
    }

    private static DateTime? ResolveNextScheduledScanAt(AdminSharedFolderCrawlMode crawlMode, DateTime referenceUtc)
    {
        return crawlMode == AdminSharedFolderCrawlMode.Scheduled
            ? referenceUtc.AddHours(24)
            : null;
    }

    private static bool TryProbeSourcePath(string sourcePath, out string? failureMessage)
    {
        failureMessage = null;

        try
        {
            if (!Directory.Exists(sourcePath))
            {
                failureMessage = "Source path does not exist.";
                return false;
            }

            _ = Directory.EnumerateFileSystemEntries(sourcePath).Take(1).ToArray();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failureMessage = ex.Message;
            return false;
        }
    }
}