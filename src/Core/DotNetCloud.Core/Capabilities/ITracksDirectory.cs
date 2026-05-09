using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read-only access to products and work items for cross-module references.
/// Modules use this capability to resolve product/work-item links without direct data access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability tier:</b> Public — automatically granted to all modules.
/// </para>
/// <para>
/// This capability exposes a minimal read-only view of products and work items. Modules that
/// need to create or modify products/work items must use the Tracks module API directly.
/// </para>
/// <para>
/// <b>Optional module:</b> The Tracks module may not be installed. Callers should
/// handle null/empty results gracefully.
/// </para>
/// </remarks>
public interface ITracksDirectory : ICapabilityInterface
{
    /// <summary>
    /// Gets the title of a product by its ID.
    /// </summary>
    /// <param name="productId">The product ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The product title if found; otherwise <c>null</c>.</returns>
    Task<string?> GetProductTitleAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets titles for a batch of product IDs.
    /// IDs that do not map to a product are omitted from the result.
    /// </summary>
    /// <param name="productIds">The product IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetProductTitlesAsync(
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the title of a work item by its ID.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The work item title if found; otherwise <c>null</c>.</returns>
    Task<string?> GetWorkItemTitleAsync(Guid workItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets titles for a batch of work item IDs.
    /// IDs that do not map to a work item are omitted from the result.
    /// </summary>
    /// <param name="workItemIds">The work item IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetWorkItemTitlesAsync(
        IEnumerable<Guid> workItemIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches products accessible to a user by title (case-insensitive substring match).
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="query">Search query string.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Product IDs and titles matching the query.</returns>
    Task<IReadOnlyList<(Guid ProductId, string Title)>> SearchProductsAsync(
        Guid userId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches work items accessible to a user by title (case-insensitive substring match).
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="query">Search query string.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Work item summaries matching the query.</returns>
    Task<IReadOnlyList<WorkItemSummary>> SearchWorkItemsAsync(
        Guid userId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight summary of a work item for cross-module display.
/// </summary>
public sealed record WorkItemSummary
{
    /// <summary>
    /// The work item's unique identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The work item's title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The product the work item belongs to.
    /// </summary>
    public required Guid ProductId { get; init; }

    /// <summary>
    /// The product's title.
    /// </summary>
    public required string ProductTitle { get; init; }

    /// <summary>
    /// The work item's priority.
    /// </summary>
    public Priority Priority { get; init; }

    /// <summary>
    /// The work item's due date, if any.
    /// </summary>
    public DateTime? DueDate { get; init; }
}
