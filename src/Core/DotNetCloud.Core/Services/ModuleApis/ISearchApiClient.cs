using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>
/// gRPC API client interface for the Search module.
/// </summary>
public interface ISearchApiClient
{
    /// <summary>
    /// Triggers a full reindex of the specified module.
    /// </summary>
    /// <param name="moduleId">The module ID to reindex (e.g., "files", "tracks").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the reindex was successfully triggered.</returns>
    Task<bool> ReindexModuleAsync(string moduleId, CancellationToken ct = default);

    /// <summary>
    /// Adds or updates a document in the search index.
    /// </summary>
    /// <param name="document">The document to index.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the document was successfully indexed.</returns>
    Task<bool> IndexDocumentAsync(SearchDocument document, CancellationToken ct = default);

    /// <summary>
    /// Removes a document from the search index.
    /// </summary>
    /// <param name="moduleId">The source module identifier.</param>
    /// <param name="entityId">The entity identifier within the module.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the document was successfully removed.</returns>
    Task<bool> RemoveDocumentAsync(string moduleId, string entityId, CancellationToken ct = default);

    /// <summary>
    /// Gets the current search index statistics.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Index statistics.</returns>
    Task<IndexStats> GetIndexStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// Search index statistics returned by <see cref="ISearchApiClient.GetIndexStatsAsync"/>.
/// </summary>
public sealed record IndexStats(int TotalDocuments, int TotalModules);

