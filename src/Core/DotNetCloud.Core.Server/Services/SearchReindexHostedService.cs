using System.Diagnostics;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Search;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Core.Search.Services;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Background service that performs scheduled full reindex operations against the
/// core-owned search index. Replaces the old Search module's reindex service and the
/// initial-index logic that previously lived in <see cref="SearchEventSubscriber"/>.
/// Pulls documents from each module over gRPC via <see cref="IModuleSearchDocumentClient"/>
/// and indexes via <see cref="ISearchProvider"/> (which uses <see cref="CoreDbContext"/>).
/// </summary>
public sealed class SearchReindexHostedService : BackgroundService
{
    private const string ServiceName = "Search Full Reindex";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SearchReindexHostedService> _logger;
    private readonly IBackgroundServiceTracker _tracker;
    private readonly SearchIndexingService _indexingService;
    private readonly TimeSpan _reindexInterval;
    private readonly SemaphoreSlim _triggerSemaphore = new(0, 1);
    private volatile bool _manualReindexRequested;
    private volatile string? _manualReindexModuleId;

    // ── Live progress tracking (read by admin status endpoint) ──────────
    private volatile bool _isReindexing;
    private volatile string? _currentModuleId;
    private int _reindexDocumentsProcessed;
    private int _reindexDocumentsTotal;
    private long _reindexStartedAtTicks;

    /// <summary>Whether a full reindex is currently running.</summary>
    public bool IsReindexing => _isReindexing;

    /// <summary>Module currently being reindexed, or null.</summary>
    public string? CurrentModuleId => _currentModuleId;

    /// <summary>Documents processed so far in the current reindex run.</summary>
    public int ReindexDocumentsProcessed => Interlocked.CompareExchange(ref _reindexDocumentsProcessed, 0, 0);

    /// <summary>Total documents to process in the current reindex run.</summary>
    public int ReindexDocumentsTotal => Interlocked.CompareExchange(ref _reindexDocumentsTotal, 0, 0);

    /// <summary>When the current reindex run started, or null.</summary>
    public DateTimeOffset? ReindexStartedAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _reindexStartedAtTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    /// <summary>Default batch size for indexing operations during full reindex.</summary>
    public const int DefaultBatchSize = 200;

    /// <summary>Maximum time a single reindex operation may run before being cancelled.</summary>
    private static readonly TimeSpan ReindexTimeout = TimeSpan.FromHours(1);

    /// <summary>Initializes a new instance of the <see cref="SearchReindexHostedService"/> class.</summary>
    public SearchReindexHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SearchReindexHostedService> logger,
        IBackgroundServiceTracker tracker,
        SearchIndexingService indexingService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _tracker = tracker;
        _indexingService = indexingService;
        _reindexInterval = TimeSpan.FromHours(24); // Default: daily
    }

    /// <summary>
    /// Triggers an on-demand full reindex of all modules.
    /// </summary>
    public void TriggerFullReindex()
    {
        _manualReindexModuleId = null;
        _manualReindexRequested = true;
        try
        { _triggerSemaphore.Release(); }
        catch (SemaphoreFullException) { /* already signaled */ }
    }

    /// <summary>
    /// Triggers an on-demand reindex for a specific module.
    /// </summary>
    public void TriggerModuleReindex(string moduleId)
    {
        _manualReindexModuleId = moduleId;
        _manualReindexRequested = true;
        try
        { _triggerSemaphore.Release(); }
        catch (SemaphoreFullException) { /* already signaled */ }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Search reindex background service started (interval: {Interval})", _reindexInterval);

        // Start the channel-backed real-time indexing queue.
        _indexingService.Start();

        // Wait for initial startup to complete
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        // Run an immediate full reindex on startup, then wait for the interval
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Starting initial full reindex after startup");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(ReindexTimeout);
            await PerformFullReindexAsync(cts.Token);
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: true);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: false, message: "Timed out");
            _logger.LogWarning("Initial reindex timed out after {Timeout} and was cancelled", ReindexTimeout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: false, message: ex.Message);
            _logger.LogError(ex, "Initial reindex operation failed");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for either the interval to elapse or a manual trigger
                var triggered = await WaitForNextRunAsync(stoppingToken);
                sw = Stopwatch.StartNew();

                if (triggered && _manualReindexRequested)
                {
                    _manualReindexRequested = false;
                    var moduleId = _manualReindexModuleId;
                    _manualReindexModuleId = null;

                    if (moduleId is not null)
                    {
                        _logger.LogInformation("Starting manual module reindex for {ModuleId}", moduleId);
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        cts.CancelAfter(ReindexTimeout);
                        await PerformModuleReindexAsync(moduleId, cts.Token);
                    }
                    else
                    {
                        _logger.LogInformation("Starting manual full reindex");
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        cts.CancelAfter(ReindexTimeout);
                        await PerformFullReindexAsync(cts.Token);
                    }
                }
                else
                {
                    _logger.LogInformation("Starting scheduled full reindex cycle");
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(ReindexTimeout);
                    await PerformFullReindexAsync(cts.Token);
                }

                sw.Stop();
                _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: true);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                sw.Stop();
                _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: false, message: "Timed out");
                _logger.LogWarning("Reindex operation timed out after {Timeout} and was cancelled", ReindexTimeout);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sw.Stop();
                _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: false, message: ex.Message);
                _logger.LogError(ex, "Reindex operation failed");
            }
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await _indexingService.StopAsync();
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
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var searchProvider = scope.ServiceProvider.GetRequiredService<ISearchProvider>();
        var documentClients = scope.ServiceProvider.GetServices<IModuleSearchDocumentClient>();

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

        // Set live progress tracking
        _isReindexing = true;
        Interlocked.Exchange(ref _reindexStartedAtTicks, DateTimeOffset.UtcNow.UtcTicks);
        Interlocked.Exchange(ref _reindexDocumentsProcessed, 0);
        Interlocked.Exchange(ref _reindexDocumentsTotal, 0);

        try
        {
            // First pass: pull documents from every module over gRPC
            var moduleDocuments = new List<(string ModuleId, IReadOnlyList<SearchDocument> Documents)>();
            foreach (var client in documentClients)
            {
                try
                {
                    var documents = await client.GetAllSearchableDocumentsAsync(cancellationToken);
                    moduleDocuments.Add((client.ModuleId, documents));
                    totalDocuments += documents.Count;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to retrieve documents for module {ModuleId}", client.ModuleId);
                }
            }

            Interlocked.Exchange(ref _reindexDocumentsTotal, totalDocuments);

            foreach (var (moduleId, documents) in moduleDocuments)
            {
                _currentModuleId = moduleId;
                _logger.LogInformation("Reindexing module {ModuleId}", moduleId);

                try
                {
                    // Clear existing entries for this module, then batch-index the pulled
                    // documents (batched SaveChanges — fast enough to finish within the
                    // reindex timeout even for modules with tens of thousands of entries).
                    await searchProvider.ReindexModuleAsync(moduleId, cancellationToken);
                    var processed = await IndexBatchAsync(searchProvider, documents, cancellationToken);
                    totalProcessed += processed;
                    Interlocked.Exchange(ref _reindexDocumentsProcessed, totalProcessed);

                    _logger.LogInformation("Module {ModuleId}: indexed {Count} documents",
                        moduleId, documents.Count);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to reindex module {ModuleId}", moduleId);
                }
            }

            // Remove index entries for modules that are no longer pullable (e.g. chat,
            // contacts, photos, tracks) — they are not in the document-client set and
            // their historical entries are stale after a full reindex.
            await CleanupOrphanedEntriesAsync(db, documentClients, cancellationToken);

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
            _isReindexing = false;
            _currentModuleId = null;
            Interlocked.Exchange(ref _reindexStartedAtTicks, 0);
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
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var searchProvider = scope.ServiceProvider.GetRequiredService<ISearchProvider>();
        var documentClients = scope.ServiceProvider.GetServices<IModuleSearchDocumentClient>();

        var client = documentClients.FirstOrDefault(c => c.ModuleId == moduleId);
        if (client is null)
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

            var documents = await client.GetAllSearchableDocumentsAsync(cancellationToken);

            // Clear existing entries for this module, then batch-index the pulled documents.
            await searchProvider.ReindexModuleAsync(moduleId, cancellationToken);
            totalProcessed = await IndexBatchAsync(searchProvider, documents, cancellationToken);

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
    /// Indexes a batch of documents efficiently. Uses the provider's batched upsert when
    /// available (one SaveChanges per batch instead of per document); otherwise falls back
    /// to per-document indexing.
    /// </summary>
    private static async Task<int> IndexBatchAsync(
        ISearchProvider searchProvider,
        IReadOnlyList<SearchDocument> documents,
        CancellationToken cancellationToken)
    {
        if (searchProvider is SqlServerSearchProvider sqlServer)
            return await sqlServer.BatchIndexAsync(documents, cancellationToken);
        if (searchProvider is PostgreSqlSearchProvider postgreSql)
            return await postgreSql.BatchIndexAsync(documents, cancellationToken);

        var count = 0;
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await searchProvider.IndexDocumentAsync(document, cancellationToken);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Removes index entries for modules that are no longer pullable (i.e. not among the
    /// registered document clients). Their historical entries are stale after a full reindex.
    /// </summary>
    private static async Task CleanupOrphanedEntriesAsync(
        CoreDbContext db,
        IEnumerable<IModuleSearchDocumentClient> documentClients,
        CancellationToken cancellationToken)
    {
        var clientModuleIds = new HashSet<string>(
            documentClients.Select(c => c.ModuleId),
            StringComparer.OrdinalIgnoreCase);

        var indexedModuleIds = await db.SearchIndexEntries
            .Select(e => e.ModuleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var moduleId in indexedModuleIds.Where(id => !clientModuleIds.Contains(id)))
        {
            var staleEntries = await db.SearchIndexEntries
                .Where(e => e.ModuleId == moduleId)
                .ToListAsync(cancellationToken);
            if (staleEntries.Count == 0)
                continue;

            db.SearchIndexEntries.RemoveRange(staleEntries);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
