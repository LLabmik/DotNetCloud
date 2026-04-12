using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// Executes search queries against the search index via the configured <see cref="ISearchProvider"/>.
/// Provides a single entry point for all search operations.
/// </summary>
public sealed class SearchQueryService
{
    private readonly ISearchProvider _searchProvider;
    private readonly ILogger<SearchQueryService> _logger;

    /// <summary>Initializes a new instance of the <see cref="SearchQueryService"/> class.</summary>
    public SearchQueryService(ISearchProvider searchProvider, ILogger<SearchQueryService> logger)
    {
        _searchProvider = searchProvider;
        _logger = logger;
    }

    /// <summary>Executes a full-text search query.</summary>
    public async Task<SearchResultDto> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.QueryText))
        {
            return new SearchResultDto
            {
                Items = [],
                TotalCount = 0,
                Page = query.Page,
                PageSize = query.PageSize,
                FacetCounts = new Dictionary<string, int>()
            };
        }

        _logger.LogDebug("Executing search: \"{QueryText}\" (module={Module}, type={Type}, page={Page})",
            query.QueryText, query.ModuleFilter ?? "all", query.EntityTypeFilter ?? "all", query.Page);

        var result = await _searchProvider.SearchAsync(query, cancellationToken);

        _logger.LogDebug("Search returned {Count} results (total: {Total})",
            result.Items.Count, result.TotalCount);

        return result;
    }

    /// <summary>Returns search index statistics.</summary>
    public Task<SearchIndexStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return _searchProvider.GetIndexStatsAsync(cancellationToken);
    }

    /// <summary>Triggers a full reindex for a specific module.</summary>
    public async Task ReindexModuleAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Triggering reindex for module {ModuleId}", moduleId);
        await _searchProvider.ReindexModuleAsync(moduleId, cancellationToken);
    }
}
