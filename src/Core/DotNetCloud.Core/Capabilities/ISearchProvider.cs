using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provider-agnostic full-text search abstraction.
/// Implementations use native database FTS capabilities (PostgreSQL tsvector,
/// SQL Server Full-Text Index, MariaDB FULLTEXT INDEX).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tier:</b> Restricted — requires administrator approval.
/// </para>
/// <para>
/// The Search module uses one <see cref="ISearchProvider"/> implementation at a time,
/// selected automatically based on the configured database provider.
/// </para>
/// </remarks>
public interface ISearchProvider : ICapabilityInterface
{
    /// <summary>
    /// Adds or updates a document in the search index.
    /// If a document with the same <see cref="SearchDocument.ModuleId"/> and
    /// <see cref="SearchDocument.EntityId"/> already exists, it is replaced.
    /// </summary>
    /// <param name="document">The document to index.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document from the search index.
    /// </summary>
    /// <param name="moduleId">The source module identifier.</param>
    /// <param name="entityId">The entity identifier within the module.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RemoveDocumentAsync(string moduleId, string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a full-text search query against the index.
    /// Results are permission-scoped to the user specified in the query.
    /// </summary>
    /// <param name="query">The search query with filters and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A paginated, relevance-ranked result set.</returns>
    Task<SearchResultDto> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a full reindex for the specified module, replacing all existing entries.
    /// </summary>
    /// <param name="moduleId">The module to reindex.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ReindexModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns health and statistics information about the search index.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Index statistics including document counts per module and last index time.</returns>
    Task<SearchIndexStats> GetIndexStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about the current state of the search index.
/// </summary>
public sealed record SearchIndexStats
{
    /// <summary>Total number of documents in the index.</summary>
    public required int TotalDocuments { get; init; }

    /// <summary>Number of documents per module.</summary>
    public required IReadOnlyDictionary<string, int> DocumentsPerModule { get; init; }

    /// <summary>When the last full reindex completed, if ever.</summary>
    public DateTimeOffset? LastFullReindexAt { get; init; }

    /// <summary>When the last incremental index operation completed.</summary>
    public DateTimeOffset? LastIncrementalIndexAt { get; init; }
}
