using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read-only access to boards and cards for cross-module references.
/// Modules use this capability to resolve board/card links without direct data access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability tier:</b> Public — automatically granted to all modules.
/// </para>
/// <para>
/// This capability exposes a minimal read-only view of boards and cards. Modules that
/// need to create or modify boards/cards must use the Tracks module API directly.
/// </para>
/// <para>
/// <b>Optional module:</b> The Tracks module may not be installed. Callers should
/// handle null/empty results gracefully.
/// </para>
/// </remarks>
public interface ITracksDirectory : ICapabilityInterface
{
    /// <summary>
    /// Gets the title of a board by its ID.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The board title if found; otherwise <c>null</c>.</returns>
    Task<string?> GetBoardTitleAsync(Guid boardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets titles for a batch of board IDs.
    /// IDs that do not map to a board are omitted from the result.
    /// </summary>
    /// <param name="boardIds">The board IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetBoardTitlesAsync(
        IEnumerable<Guid> boardIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the title of a card by its ID.
    /// </summary>
    /// <param name="cardId">The card ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The card title if found; otherwise <c>null</c>.</returns>
    Task<string?> GetCardTitleAsync(Guid cardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets titles for a batch of card IDs.
    /// IDs that do not map to a card are omitted from the result.
    /// </summary>
    /// <param name="cardIds">The card IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetCardTitlesAsync(
        IEnumerable<Guid> cardIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches boards accessible to a user by title (case-insensitive substring match).
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="query">Search query string.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Board IDs and titles matching the query.</returns>
    Task<IReadOnlyList<(Guid BoardId, string Title)>> SearchBoardsAsync(
        Guid userId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches cards accessible to a user by title (case-insensitive substring match).
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="query">Search query string.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Card summaries matching the query.</returns>
    Task<IReadOnlyList<CardSummary>> SearchCardsAsync(
        Guid userId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight summary of a card for cross-module display.
/// </summary>
public sealed record CardSummary
{
    /// <summary>
    /// The card's unique identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The card's title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The board the card belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The board's title.
    /// </summary>
    public required string BoardTitle { get; init; }

    /// <summary>
    /// The card's priority.
    /// </summary>
    public CardPriority Priority { get; init; }

    /// <summary>
    /// The card's due date, if any.
    /// </summary>
    public DateTime? DueDate { get; init; }
}
