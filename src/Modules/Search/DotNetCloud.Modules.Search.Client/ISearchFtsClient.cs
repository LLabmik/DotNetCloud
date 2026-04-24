using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Modules.Search.Client;

/// <summary>
/// Client interface for calling the Search module's full-text search gRPC service.
/// Modules inject this to delegate search operations to the centralized FTS engine.
/// </summary>
public interface ISearchFtsClient
{
    /// <summary>
    /// Indicates whether the Search module gRPC service is available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Executes a full-text search query against the Search module.
    /// </summary>
    /// <param name="queryText">The search query text.</param>
    /// <param name="moduleFilter">Optional module ID filter (e.g., "files", "notes").</param>
    /// <param name="entityTypeFilter">Optional entity type filter (e.g., "Note", "Message").</param>
    /// <param name="userId">Authenticated user ID for permission scoping.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Results per page.</param>
    /// <param name="sortOrder">Sort order for results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results or null if the service is unavailable.</returns>
    Task<SearchResultDto?> SearchAsync(
        string queryText,
        string? moduleFilter = null,
        string? entityTypeFilter = null,
        Guid? userId = null,
        int page = 1,
        int pageSize = 20,
        SearchSortOrder sortOrder = SearchSortOrder.Relevance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a full reindex for a specific module.
    /// </summary>
    /// <param name="moduleId">The module identifier to reindex.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the Search module accepted the request; otherwise <see langword="false"/>.</returns>
    Task<bool> RequestModuleReindexAsync(string moduleId, CancellationToken cancellationToken = default);
}
