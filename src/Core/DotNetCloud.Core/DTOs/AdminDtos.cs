namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Query parameters for listing users with pagination and filtering.
/// </summary>
public sealed class UserListQuery
{
    /// <summary>
    /// Gets or sets the page number (1-based). Defaults to 1.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size. Defaults to 25.
    /// </summary>
    public int PageSize { get; set; } = 25;

    /// <summary>
    /// Gets or sets the search term (matches email or display name).
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the sort field. Defaults to "email".
    /// </summary>
    public string SortBy { get; set; } = "email";

    /// <summary>
    /// Gets or sets the sort direction. Defaults to "asc".
    /// </summary>
    public string SortDirection { get; set; } = "asc";

    /// <summary>
    /// Gets or sets a filter for active/inactive status. Null returns all.
    /// </summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// Represents a paginated result set.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
public sealed class PaginatedResult<T>
{
    /// <summary>
    /// Gets or sets the items on the current page.
    /// </summary>
    public IReadOnlyList<T> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the total number of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

/// <summary>
/// Request for an admin-initiated password reset (no current password required).
/// </summary>
public sealed class AdminResetPasswordRequest
{
    /// <summary>
    /// Gets or sets the new password for the user.
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;
}
