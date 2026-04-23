using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Search.Host.Controllers;

/// <summary>
/// REST API controller for search operations.
/// Provides global search, autocomplete suggestions, index statistics, and admin reindex triggers.
/// </summary>
[Route("api/v1/search")]
public sealed class SearchController : SearchControllerBase
{
    private readonly SearchQueryService _queryService;
    private readonly IGroupDirectory? _groupDirectory;
    private readonly SearchReindexBackgroundService? _reindexService;
    private readonly SearchIndexingService? _indexingService;
    private readonly SearchDbContext _db;
    private readonly ILogger<SearchController> _logger;

    /// <summary>Initializes a new instance of the <see cref="SearchController"/> class.</summary>
    public SearchController(
        SearchQueryService queryService,
        SearchDbContext db,
        ILogger<SearchController> logger,
        IGroupDirectory? groupDirectory = null,
        SearchReindexBackgroundService? reindexService = null,
        SearchIndexingService? indexingService = null)
    {
        _queryService = queryService;
        _db = db;
        _logger = logger;
        _groupDirectory = groupDirectory;
        _reindexService = reindexService;
        _indexingService = indexingService;
    }

    /// <summary>
    /// Executes a global full-text search.
    /// </summary>
    /// <param name="q">Search query text.</param>
    /// <param name="module">Optional module filter (e.g., "files", "notes").</param>
    /// <param name="type">Optional entity type filter (e.g., "Note", "Message").</param>
    /// <param name="page">Page number (1-based, default 1).</param>
    /// <param name="pageSize">Results per page (default 20, max 100).</param>
    /// <param name="sort">Sort order: "relevance", "date_desc", "date_asc".</param>
    [HttpGet]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] string q,
        [FromQuery] string? module = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "relevance")
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(ErrorEnvelope("INVALID_QUERY", "Search query is required."));
        }

        var caller = GetAuthenticatedCaller();

        var groupIds = await GetCallerGroupIdsAsync(caller.UserId, HttpContext.RequestAborted);

        var sortOrder = sort?.ToLowerInvariant() switch
        {
            "date_desc" => SearchSortOrder.DateDesc,
            "date_asc" => SearchSortOrder.DateAsc,
            _ => SearchSortOrder.Relevance
        };

        var query = new SearchQuery
        {
            QueryText = q,
            ModuleFilter = module,
            EntityTypeFilter = type,
            UserId = caller.UserId,
            GroupIds = groupIds,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            SortOrder = sortOrder
        };

        var result = await _queryService.SearchAsync(query);
        return Ok(Envelope(result));
    }

    /// <summary>
    /// Returns autocomplete suggestions for a search prefix.
    /// </summary>
    /// <param name="q">Search prefix text.</param>
    [HttpGet("suggest")]
    public async Task<IActionResult> SuggestAsync([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(Envelope(Array.Empty<SearchResultItem>()));
        }

        var caller = GetAuthenticatedCaller();

        var groupIds = await GetCallerGroupIdsAsync(caller.UserId, HttpContext.RequestAborted);

        var query = new SearchQuery
        {
            QueryText = q,
            UserId = caller.UserId,
            GroupIds = groupIds,
            Page = 1,
            PageSize = 10,
            SortOrder = SearchSortOrder.Relevance
        };

        var result = await _queryService.SearchAsync(query);

        // Return just the top items for suggestions
        return Ok(Envelope(result.Items.Take(10)));
    }

    private async Task<IReadOnlyList<Guid>> GetCallerGroupIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_groupDirectory is null)
        {
            return [];
        }

        var groups = await _groupDirectory.GetGroupsForUserAsync(userId, cancellationToken);
        return groups.Select(group => group.Id).Distinct().ToArray();
    }

    /// <summary>
    /// Returns search index statistics. Admin only.
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetStatsAsync()
    {
        var stats = await _queryService.GetStatsAsync();
        return Ok(Envelope(stats));
    }

    /// <summary>
    /// Triggers a full reindex of all modules. Admin only.
    /// </summary>
    [HttpPost("admin/reindex")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> ReindexAllAsync()
    {
        var caller = GetAuthenticatedCaller();

        _logger.LogInformation("Admin {UserId} triggered full reindex", caller.UserId);

        if (_reindexService is not null)
        {
            _reindexService.TriggerFullReindex();
        }

        return Ok(Envelope(new { message = "Full reindex queued." }));
    }

    /// <summary>
    /// Triggers a reindex for a specific module. Admin only.
    /// </summary>
    [HttpPost("admin/reindex/{moduleId}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> ReindexModuleAsync(string moduleId)
    {
        var caller = GetAuthenticatedCaller();

        _logger.LogInformation("Admin {UserId} triggered reindex for module {ModuleId}", caller.UserId, moduleId);

        if (_reindexService is not null)
        {
            _reindexService.TriggerModuleReindex(moduleId);
        }
        else
        {
            // Fallback: direct reindex via query service
            await _queryService.ReindexModuleAsync(moduleId);
        }

        return Ok(Envelope(new { message = $"Reindex for module '{moduleId}' queued." }));
    }

    /// <summary>
    /// Returns comprehensive search admin status including index stats, queue depth,
    /// last job info, and live reindex progress. Admin only.
    /// </summary>
    [HttpGet("admin/status")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetAdminStatusAsync()
    {
        var stats = await _queryService.GetStatsAsync();

        // Get the most recent completed full reindex job
        var lastFullJob = await _db.IndexingJobs
            .Where(j => j.Type == Data.Models.IndexJobType.Full && j.Status == Data.Models.IndexJobStatus.Completed)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefaultAsync();

        // Get the most recent job of any kind (for "last run" display)
        var lastJob = await _db.IndexingJobs
            .OrderByDescending(j => j.StartedAt)
            .FirstOrDefaultAsync();

        // Build reindex progress if one is currently running
        object? reindexProgress = null;
        if (_reindexService?.IsReindexing == true)
        {
            reindexProgress = new
            {
                currentModule = _reindexService.CurrentModuleId,
                documentsProcessed = _reindexService.ReindexDocumentsProcessed,
                documentsTotal = _reindexService.ReindexDocumentsTotal,
                startedAt = _reindexService.ReindexStartedAt
            };
        }

        var result = new
        {
            totalDocuments = stats.TotalDocuments,
            documentsPerModule = stats.DocumentsPerModule,
            lastFullReindexAt = stats.LastFullReindexAt,
            lastIncrementalIndexAt = stats.LastIncrementalIndexAt,
            pendingQueueCount = _indexingService?.PendingCount ?? 0,
            realtimeProcessed = _indexingService?.TotalProcessed ?? 0L,
            realtimeFailed = _indexingService?.TotalFailed ?? 0L,
            isReindexing = _reindexService?.IsReindexing ?? false,
            reindexProgress,
            lastJob = lastJob is not null ? new
            {
                id = lastJob.Id,
                moduleId = lastJob.ModuleId,
                type = lastJob.Type.ToString(),
                status = lastJob.Status.ToString(),
                startedAt = lastJob.StartedAt,
                completedAt = lastJob.CompletedAt,
                documentsProcessed = lastJob.DocumentsProcessed,
                documentsTotal = lastJob.DocumentsTotal,
                errorMessage = lastJob.ErrorMessage
            } : null,
            lastFullReindexJob = lastFullJob is not null ? new
            {
                completedAt = lastFullJob.CompletedAt,
                documentsProcessed = lastFullJob.DocumentsProcessed,
                documentsTotal = lastFullJob.DocumentsTotal,
                durationSeconds = lastFullJob.StartedAt.HasValue && lastFullJob.CompletedAt.HasValue
                    ? (lastFullJob.CompletedAt.Value - lastFullJob.StartedAt.Value).TotalSeconds
                    : (double?)null
            } : null
        };

        return Ok(Envelope(result));
    }
}
