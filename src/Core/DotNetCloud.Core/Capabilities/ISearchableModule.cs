using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Capability interface that modules implement to expose their data for full-text search indexing.
/// The Search module calls these methods to pull searchable content during indexing operations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tier:</b> Public — automatically available to the Search module.
/// </para>
/// <para>
/// Each module that contains searchable data implements this interface.
/// The Search module calls <see cref="GetAllSearchableDocumentsAsync"/> during full reindex
/// and <see cref="GetSearchableDocumentAsync"/> for single-item re-indexing triggered by events.
/// </para>
/// </remarks>
public interface ISearchableModule : ICapabilityInterface
{
    /// <summary>
    /// Gets the unique module identifier (e.g., "files", "notes", "chat").
    /// </summary>
    string ModuleId { get; }

    /// <summary>
    /// Gets the entity types this module provides for search indexing.
    /// </summary>
    IReadOnlyCollection<string> SupportedEntityTypes { get; }

    /// <summary>
    /// Returns all searchable documents for a full reindex operation.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>All indexable documents from this module.</returns>
    Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single searchable document by entity ID, used for incremental indexing.
    /// </summary>
    /// <param name="entityId">The entity identifier to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The searchable document, or null if the entity no longer exists.</returns>
    Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken cancellationToken = default);
}
