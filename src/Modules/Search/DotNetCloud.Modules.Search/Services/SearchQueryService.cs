using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// Executes search queries against the search index via the configured <see cref="ISearchProvider"/>.
/// Parses user input using <see cref="SearchQueryParser"/> to support advanced syntax
/// (quoted phrases, <c>in:module</c>, <c>type:value</c>, <c>-exclusions</c>).
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

    /// <summary>Executes a full-text search query with advanced query syntax parsing.</summary>
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

        // Parse the raw query text into structured components
        var parsed = SearchQueryParser.Parse(query.QueryText);

        // If parsed query has no searchable terms (only filters/exclusions), return empty
        if (!parsed.HasSearchableContent)
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

        // Apply in:module and type:value filters from parsed syntax (override explicit filters)
        var effectiveQuery = query with
        {
            ModuleFilter = parsed.ModuleFilter ?? query.ModuleFilter,
            EntityTypeFilter = parsed.TypeFilter ?? query.EntityTypeFilter
        };

        _logger.LogDebug(
            "Executing search: \"{QueryText}\" (terms={Terms}, phrases={Phrases}, exclusions={Exclusions}, module={Module}, type={Type}, page={Page})",
            query.QueryText,
            string.Join(", ", parsed.Terms),
            string.Join(", ", parsed.Phrases),
            string.Join(", ", parsed.Exclusions),
            effectiveQuery.ModuleFilter ?? "all",
            effectiveQuery.EntityTypeFilter ?? "all",
            effectiveQuery.Page);

        var result = await _searchProvider.SearchAsync(effectiveQuery, cancellationToken);

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
