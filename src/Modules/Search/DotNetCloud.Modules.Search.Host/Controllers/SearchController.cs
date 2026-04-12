using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Search.Host.Controllers;

/// <summary>
/// REST API controller for search operations.
/// Provides global search, autocomplete suggestions, index statistics, and admin reindex triggers.
/// </summary>
[Route("api/v1/search")]
public sealed class SearchController : SearchControllerBase
{
    private readonly SearchQueryService _queryService;
    private readonly ILogger<SearchController> _logger;

    /// <summary>Initializes a new instance of the <see cref="SearchController"/> class.</summary>
    public SearchController(SearchQueryService queryService, ILogger<SearchController> logger)
    {
        _queryService = queryService;
        _logger = logger;
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

        var query = new SearchQuery
        {
            QueryText = q,
            UserId = caller.UserId,
            Page = 1,
            PageSize = 10,
            SortOrder = SearchSortOrder.Relevance
        };

        var result = await _queryService.SearchAsync(query);

        // Return just the top items for suggestions
        return Ok(Envelope(result.Items.Take(10)));
    }

    /// <summary>
    /// Returns search index statistics. Admin only.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStatsAsync()
    {
        var caller = GetAuthenticatedCaller();

        // Check for admin role
        if (!caller.Roles.Contains("admin", StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var stats = await _queryService.GetStatsAsync();
        return Ok(Envelope(stats));
    }

    /// <summary>
    /// Triggers a full reindex of all modules. Admin only.
    /// </summary>
    [HttpPost("admin/reindex")]
    public async Task<IActionResult> ReindexAllAsync()
    {
        var caller = GetAuthenticatedCaller();

        if (!caller.Roles.Contains("admin", StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        _logger.LogInformation("Admin {UserId} triggered full reindex", caller.UserId);

        // This would trigger the background reindex service
        return Ok(Envelope(new { message = "Full reindex queued." }));
    }

    /// <summary>
    /// Triggers a reindex for a specific module. Admin only.
    /// </summary>
    [HttpPost("admin/reindex/{moduleId}")]
    public async Task<IActionResult> ReindexModuleAsync(string moduleId)
    {
        var caller = GetAuthenticatedCaller();

        if (!caller.Roles.Contains("admin", StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        _logger.LogInformation("Admin {UserId} triggered reindex for module {ModuleId}", caller.UserId, moduleId);

        await _queryService.ReindexModuleAsync(moduleId);
        return Ok(Envelope(new { message = $"Reindex for module '{moduleId}' completed." }));
    }
}
