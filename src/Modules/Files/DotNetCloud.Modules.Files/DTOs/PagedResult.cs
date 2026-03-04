namespace DotNetCloud.Modules.Files.DTOs;

/// <summary>
/// Generic pagination wrapper for paged query results.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>The items in the current page.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Total number of items across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; init; }

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>Whether there is a next page.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Whether there is a previous page.</summary>
    public bool HasPreviousPage => Page > 1;
}
