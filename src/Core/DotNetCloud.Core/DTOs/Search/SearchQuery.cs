namespace DotNetCloud.Core.DTOs.Search;

/// <summary>
/// Represents a full-text search request with filtering, pagination, and sort options.
/// </summary>
public sealed record SearchQuery
{
    /// <summary>The user's search text.</summary>
    public required string QueryText { get; init; }

    /// <summary>Optional module filter. Null searches across all modules.</summary>
    public string? ModuleFilter { get; init; }

    /// <summary>Optional entity type filter (e.g., "Note", "Message").</summary>
    public string? EntityTypeFilter { get; init; }

    /// <summary>The authenticated user ID for permission-scoped queries.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Groups the authenticated user belongs to for shared-visibility filtering.</summary>
    public IReadOnlyList<Guid> GroupIds { get; init; } = [];

    /// <summary>Page number (1-based).</summary>
    public int Page { get; init; } = 1;

    /// <summary>Number of results per page.</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Sort order for the result set.</summary>
    public SearchSortOrder SortOrder { get; init; } = SearchSortOrder.Relevance;
}

/// <summary>
/// Specifies the sort order for search results.
/// </summary>
public enum SearchSortOrder
{
    /// <summary>Sort by full-text relevance score (highest first).</summary>
    Relevance,

    /// <summary>Sort by last-updated date, newest first.</summary>
    DateDesc,

    /// <summary>Sort by last-updated date, oldest first.</summary>
    DateAsc
}
