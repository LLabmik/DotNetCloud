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
/// </summary>
public sealed class SearchReindexBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SearchReindexBackgroundService> _logger;
    private readonly TimeSpan _reindexInterval;

    /// <summary>Initializes a new instance of the <see cref="SearchReindexBackgroundService"/> class.</summary>
    public SearchReindexBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SearchReindexBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _reindexInterval = TimeSpan.FromHours(24); // Default: daily
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
                await PerformFullReindexAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Full reindex failed");
            }

            await Task.Delay(_reindexInterval, stoppingToken);
        }
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

        try
        {
            foreach (var module in searchableModules)
            {
                _logger.LogInformation("Reindexing module {ModuleId}", module.ModuleId);

                try
                {
                    // Clear existing entries for this module
                    await searchProvider.ReindexModuleAsync(module.ModuleId, cancellationToken);

                    // Get all documents from the module
                    var documents = await module.GetAllSearchableDocumentsAsync(cancellationToken);

                    // Index in batches
                    foreach (var document in documents)
                    {
                        await searchProvider.IndexDocumentAsync(document, cancellationToken);
                        totalProcessed++;
                    }

                    _logger.LogInformation("Module {ModuleId}: indexed {Count} documents",
                        module.ModuleId, documents.Count);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to reindex module {ModuleId}", module.ModuleId);
                }
            }

            job.Status = IndexJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.DocumentsProcessed = totalProcessed;
            job.DocumentsTotal = totalProcessed;
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

        _logger.LogInformation("Full reindex completed: {Count} documents indexed", totalProcessed);
    }
}
