using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// Background service that performs scheduled full reindex operations.
/// Iterates all registered <see cref="ISearchableModule"/> implementations and rebuilds their index entries.
/// Supports both automatic scheduled reindexing and on-demand manual triggers.
/// </summary>
public sealed class SearchReindexBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SearchReindexBackgroundService> _logger;
    private readonly TimeSpan _reindexInterval;
    private readonly SemaphoreSlim _triggerSemaphore = new(0, 1);
    private volatile bool _manualReindexRequested;
    private volatile string? _manualReindexModuleId;

    /// <summary>Default batch size for indexing operations during full reindex.</summary>
    public const int DefaultBatchSize = 200;

    /// <summary>Initializes a new instance of the <see cref="SearchReindexBackgroundService"/> class.</summary>
    public SearchReindexBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SearchReindexBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _reindexInterval = TimeSpan.FromHours(24); // Default: daily
    }

    /// <summary>
    /// Triggers an on-demand full reindex of all modules.
    /// </summary>
    public void TriggerFullReindex()
    {
        _manualReindexModuleId = null;
        _manualReindexRequested = true;
        _triggerSemaphore.Release();
    }

    /// <summary>
    /// Triggers an on-demand reindex for a specific module.
    /// </summary>
    public void TriggerModuleReindex(string moduleId)
    {
        _manualReindexModuleId = moduleId;
        _manualReindexRequested = true;
        _triggerSemaphore.Release();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Search reindex background service started (interval: {Interval})", _reindexInterval);

        // Wait for initial startup to complete
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for either the interval to elapse or a manual trigger
                var triggered = await WaitForNextRunAsync(stoppingToken);

                if (triggered && _manualReindexRequested)
                {
                    _manualReindexRequested = false;
                    var moduleId = _manualReindexModuleId;
                    _manualReindexModuleId = null;

                    if (moduleId is not null)
                    {
                        await PerformModuleReindexAsync(moduleId, stoppingToken);
                    }
                    else
                    {
                        await PerformFullReindexAsync(stoppingToken);
                    }
                }
                else
                {
                    await PerformFullReindexAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Reindex operation failed");
            }
        }
    }

    private async Task<bool> WaitForNextRunAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(_reindexInterval, linkedCts.Token);
        var triggerTask = _triggerSemaphore.WaitAsync(linkedCts.Token);

        var completed = await Task.WhenAny(delayTask, triggerTask);

        if (completed == triggerTask)
        {
            // Cancel the delay
            await linkedCts.CancelAsync();
            return true; // Manual trigger
        }

        return false; // Scheduled interval
    }

    /// <summary>
    /// Performs a full reindex of all modules. Can be called on-demand.
    /// </summary>
    public async Task PerformFullReindexAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var searchProvider = scope.ServiceProvider.GetRequiredService<ISearchProvider>();
        var searchableModules = scope.ServiceProvider.GetServices<ISearchableModule>();

        var job = new IndexingJob
        {
            Type = IndexJobType.Full,
            Status = IndexJobStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.IndexingJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var totalProcessed = 0;
        var totalDocuments = 0;

        try
        {
            foreach (var module in searchableModules)
            {
                _logger.LogInformation("Reindexing module {ModuleId}", module.ModuleId);

                try
                {
                    var documents = await module.GetAllSearchableDocumentsAsync(cancellationToken);
                    totalDocuments += documents.Count;

                    // Collect entity IDs for stale detection
                    var freshEntityIds = new HashSet<string>(documents.Select(d => d.EntityId));

                    // Clear existing entries for this module
                    await searchProvider.ReindexModuleAsync(module.ModuleId, cancellationToken);

                    // Index in batches
                    foreach (var batch in documents.Chunk(DefaultBatchSize))
                    {
                        foreach (var document in batch)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await searchProvider.IndexDocumentAsync(document, cancellationToken);
                            totalProcessed++;
                        }
                    }

                    _logger.LogInformation("Module {ModuleId}: indexed {Count} documents",
                        module.ModuleId, documents.Count);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to reindex module {ModuleId}", module.ModuleId);
                }
            }

            // Clean up stale entries from modules that are no longer registered
            await CleanupOrphanedEntriesAsync(db, searchableModules, cancellationToken);

            job.Status = IndexJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.DocumentsProcessed = totalProcessed;
            job.DocumentsTotal = totalDocuments;
        }
        catch (Exception ex)
        {
            job.Status = IndexJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = ex.Message;
            job.DocumentsProcessed = totalProcessed;
            job.DocumentsTotal = totalDocuments;
            throw;
        }
        finally
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }

        _logger.LogInformation("Full reindex completed: {Count}/{Total} documents indexed",
            totalProcessed, totalDocuments);
    }

    /// <summary>
    /// Performs a reindex for a specific module, tracking it with an <see cref="IndexingJob"/>.
    /// </summary>
    public async Task PerformModuleReindexAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var searchProvider = scope.ServiceProvider.GetRequiredService<ISearchProvider>();
        var searchableModules = scope.ServiceProvider.GetServices<ISearchableModule>();

        var module = searchableModules.FirstOrDefault(m => m.ModuleId == moduleId);
        if (module is null)
        {
            _logger.LogWarning("Module {ModuleId} not found for reindex", moduleId);
            return;
        }

        var job = new IndexingJob
        {
            ModuleId = moduleId,
            Type = IndexJobType.Incremental,
            Status = IndexJobStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.IndexingJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var totalProcessed = 0;

        try
        {
            _logger.LogInformation("Reindexing module {ModuleId}", moduleId);

            var documents = await module.GetAllSearchableDocumentsAsync(cancellationToken);

            // Clear existing entries for this module
            await searchProvider.ReindexModuleAsync(moduleId, cancellationToken);

            // Index in batches
            foreach (var batch in documents.Chunk(DefaultBatchSize))
            {
                foreach (var document in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await searchProvider.IndexDocumentAsync(document, cancellationToken);
                    totalProcessed++;
                }
            }

            job.Status = IndexJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.DocumentsProcessed = totalProcessed;
            job.DocumentsTotal = documents.Count;

            _logger.LogInformation("Module {ModuleId}: reindexed {Count} documents", moduleId, totalProcessed);
        }
        catch (Exception ex)
        {
            job.Status = IndexJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = ex.Message;
            job.DocumentsProcessed = totalProcessed;
            throw;
        }
        finally
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Removes index entries for modules that are no longer registered as searchable.
    /// </summary>
    internal static async Task CleanupOrphanedEntriesAsync(
        SearchDbContext db,
        IEnumerable<ISearchableModule> registeredModules,
        CancellationToken cancellationToken)
    {
        var registeredModuleIds = new HashSet<string>(registeredModules.Select(m => m.ModuleId));

        var orphanedModuleIds = await db.SearchIndexEntries
            .Select(e => e.ModuleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var moduleId in orphanedModuleIds.Where(id => !registeredModuleIds.Contains(id)))
        {
            var orphanedEntries = await db.SearchIndexEntries
                .Where(e => e.ModuleId == moduleId)
                .ToListAsync(cancellationToken);

            db.SearchIndexEntries.RemoveRange(orphanedEntries);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
