namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read-only access to notes for cross-module references.
/// Modules use this capability to resolve note links without direct data access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability tier:</b> Public — automatically granted to all modules.
/// </para>
/// <para>
/// This capability exposes a minimal read-only view of notes. Modules that
/// need to create or modify notes must use the Notes module API directly.
/// </para>
/// </remarks>
public interface INoteDirectory : ICapabilityInterface
{
    /// <summary>
    /// Gets the title of a note by its ID.
    /// </summary>
    /// <param name="noteId">The note ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The note title if found; otherwise <c>null</c>.</returns>
    Task<string?> GetNoteTitleAsync(Guid noteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets titles for a batch of note IDs.
    /// IDs that do not map to a note are omitted from the result.
    /// </summary>
    /// <param name="noteIds">The note IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetNoteTitlesAsync(
        IEnumerable<Guid> noteIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches notes owned by a user by title (case-insensitive substring match).
    /// </summary>
    /// <param name="userId">The owner's user ID.</param>
    /// <param name="query">Search query string.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Note IDs and titles matching the query.</returns>
    Task<IReadOnlyList<(Guid NoteId, string Title)>> SearchNotesAsync(
        Guid userId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}
