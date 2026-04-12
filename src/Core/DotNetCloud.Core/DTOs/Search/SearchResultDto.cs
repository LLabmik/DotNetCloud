namespace DotNetCloud.Core.DTOs.Search;

/// <summary>
/// Aggregated response from a full-text search operation.
/// </summary>
public sealed record SearchResultDto
{
    /// <summary>The matching search result items for the current page.</summary>
    public required IReadOnlyList<SearchResultItem> Items { get; init; }

    /// <summary>Total number of matching documents across all pages.</summary>
    public required int TotalCount { get; init; }

    /// <summary>Current page number (1-based).</summary>
    public required int Page { get; init; }

    /// <summary>Page size used for this query.</summary>
    public required int PageSize { get; init; }

    /// <summary>Number of matching results per module (e.g., "files": 23, "notes": 5).</summary>
    public IReadOnlyDictionary<string, int> FacetCounts { get; init; } = new Dictionary<string, int>();
}
