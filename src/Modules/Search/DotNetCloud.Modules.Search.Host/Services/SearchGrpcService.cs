using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Host.Protos;
using DotNetCloud.Modules.Search.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Host.Services;

/// <summary>
/// gRPC service implementation for the Search module.
/// Delegates to <see cref="SearchQueryService"/> and <see cref="ISearchProvider"/>.
/// </summary>
public sealed class SearchGrpcService : Protos.SearchService.SearchServiceBase
{
    private readonly SearchQueryService _queryService;
    private readonly ISearchProvider _searchProvider;
    private readonly ILogger<SearchGrpcService> _logger;

    /// <summary>Initializes a new instance of the <see cref="SearchGrpcService"/> class.</summary>
    public SearchGrpcService(
        SearchQueryService queryService,
        ISearchProvider searchProvider,
        ILogger<SearchGrpcService> logger)
    {
        _queryService = queryService;
        _searchProvider = searchProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<SearchResponse> Search(SearchRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                return new SearchResponse { Success = false, ErrorMessage = "Invalid user ID" };
            }

            var sortOrder = request.SortOrder switch
            {
                "DateDesc" => SearchSortOrder.DateDesc,
                "DateAsc" => SearchSortOrder.DateAsc,
                _ => SearchSortOrder.Relevance
            };

            var query = new SearchQuery
            {
                QueryText = request.QueryText,
                ModuleFilter = string.IsNullOrEmpty(request.ModuleFilter) ? null : request.ModuleFilter,
                EntityTypeFilter = string.IsNullOrEmpty(request.EntityTypeFilter) ? null : request.EntityTypeFilter,
                UserId = userId,
                Page = request.Page > 0 ? request.Page : 1,
                PageSize = request.PageSize > 0 ? Math.Min(request.PageSize, 100) : 20,
                SortOrder = sortOrder
            };

            var result = await _queryService.SearchAsync(query, context.CancellationToken);

            var response = new SearchResponse
            {
                Success = true,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };

            foreach (var item in result.Items)
            {
                var msg = new SearchResultItemMessage
                {
                    ModuleId = item.ModuleId,
                    EntityId = item.EntityId,
                    EntityType = item.EntityType,
                    Title = item.Title,
                    Snippet = item.Snippet,
                    RelevanceScore = item.RelevanceScore,
                    UpdatedAt = item.UpdatedAt.ToString("o")
                };

                foreach (var kvp in item.Metadata)
                {
                    msg.Metadata[kvp.Key] = kvp.Value;
                }

                response.Items.Add(msg);
            }

            foreach (var kvp in result.FacetCounts)
            {
                response.FacetCounts[kvp.Key] = kvp.Value;
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC Search failed");
            return new SearchResponse { Success = false, ErrorMessage = "Search failed" };
        }
    }

    /// <inheritdoc />
    public override async Task<IndexDocumentResponse> IndexDocument(IndexDocumentRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.OwnerId, out var ownerId))
            {
                return new IndexDocumentResponse { Success = false, ErrorMessage = "Invalid owner ID" };
            }

            Guid? orgId = Guid.TryParse(request.OrganizationId, out var parsedOrgId) ? parsedOrgId : null;

            var document = new SearchDocument
            {
                ModuleId = request.ModuleId,
                EntityId = request.EntityId,
                EntityType = request.EntityType,
                Title = request.Title,
                Content = request.Content,
                Summary = string.IsNullOrEmpty(request.Summary) ? null : request.Summary,
                OwnerId = ownerId,
                OrganizationId = orgId,
                CreatedAt = DateTimeOffset.TryParse(request.CreatedAt, out var created) ? created : DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.TryParse(request.UpdatedAt, out var updated) ? updated : DateTimeOffset.UtcNow,
                Metadata = request.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            await _searchProvider.IndexDocumentAsync(document, context.CancellationToken);

            return new IndexDocumentResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC IndexDocument failed");
            return new IndexDocumentResponse { Success = false, ErrorMessage = "Indexing failed" };
        }
    }

    /// <inheritdoc />
    public override async Task<RemoveDocumentResponse> RemoveDocument(RemoveDocumentRequest request, ServerCallContext context)
    {
        try
        {
            await _searchProvider.RemoveDocumentAsync(request.ModuleId, request.EntityId, context.CancellationToken);
            return new RemoveDocumentResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC RemoveDocument failed");
            return new RemoveDocumentResponse { Success = false, ErrorMessage = "Remove failed" };
        }
    }

    /// <inheritdoc />
    public override async Task<ReindexModuleResponse> ReindexModule(ReindexModuleRequest request, ServerCallContext context)
    {
        try
        {
            await _queryService.ReindexModuleAsync(request.ModuleId, context.CancellationToken);
            return new ReindexModuleResponse { Success = true, JobId = Guid.NewGuid().ToString() };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC ReindexModule failed");
            return new ReindexModuleResponse { Success = false, ErrorMessage = "Reindex failed" };
        }
    }

    /// <inheritdoc />
    public override async Task<IndexStatsResponse> GetIndexStats(GetIndexStatsRequest request, ServerCallContext context)
    {
        try
        {
            var stats = await _queryService.GetStatsAsync(context.CancellationToken);

            var response = new IndexStatsResponse
            {
                Success = true,
                TotalDocuments = stats.TotalDocuments,
                LastFullReindexAt = stats.LastFullReindexAt?.ToString("o") ?? string.Empty,
                LastIncrementalIndexAt = stats.LastIncrementalIndexAt?.ToString("o") ?? string.Empty
            };

            foreach (var kvp in stats.DocumentsPerModule)
            {
                response.DocumentsPerModule[kvp.Key] = kvp.Value;
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC GetIndexStats failed");
            return new IndexStatsResponse { Success = false, ErrorMessage = "Stats retrieval failed" };
        }
    }
}
