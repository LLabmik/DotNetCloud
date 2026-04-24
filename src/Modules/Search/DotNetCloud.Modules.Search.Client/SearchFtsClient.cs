using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Host.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Search.Client;

/// <summary>
/// gRPC client implementation for calling the Search module's full-text search service.
/// Falls back gracefully when the Search module is unavailable.
/// </summary>
public sealed class SearchFtsClient : ISearchFtsClient, IDisposable
{
    private readonly SearchFtsClientOptions _options;
    private readonly ILogger<SearchFtsClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<SearchService.SearchServiceClient> _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="SearchFtsClient"/> class.</summary>
    public SearchFtsClient(IOptions<SearchFtsClientOptions> options, ILogger<SearchFtsClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(() => CreateChannel());
        _client = new Lazy<SearchService.SearchServiceClient>(
            () => new SearchService.SearchServiceClient(_channel.Value));
    }

    /// <inheritdoc />
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_options.SearchModuleAddress);

    /// <inheritdoc />
    public async Task<SearchResultDto?> SearchAsync(
        string queryText,
        string? moduleFilter = null,
        string? entityTypeFilter = null,
        Guid? userId = null,
        int page = 1,
        int pageSize = 20,
        SearchSortOrder sortOrder = SearchSortOrder.Relevance,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogDebug("Search FTS client unavailable — Search module address not configured");
            return null;
        }

        try
        {
            var request = new SearchRequest
            {
                QueryText = queryText,
                ModuleFilter = moduleFilter ?? string.Empty,
                EntityTypeFilter = entityTypeFilter ?? string.Empty,
                UserId = userId?.ToString() ?? string.Empty,
                Page = page,
                PageSize = pageSize,
                SortOrder = sortOrder switch
                {
                    SearchSortOrder.DateDesc => "DateDesc",
                    SearchSortOrder.DateAsc => "DateAsc",
                    _ => "Relevance"
                }
            };

            var callOptions = CreateCallOptions(cancellationToken);

            var response = await _client.Value.SearchAsync(request, callOptions);

            if (!response.Success)
            {
                _logger.LogWarning("Search gRPC call failed: {Error}", response.ErrorMessage);
                return null;
            }

            var items = response.Items.Select(item => new SearchResultItem
            {
                ModuleId = item.ModuleId,
                EntityId = item.EntityId,
                EntityType = item.EntityType,
                Title = item.Title,
                Snippet = item.Snippet,
                RelevanceScore = item.RelevanceScore,
                UpdatedAt = DateTimeOffset.TryParse(item.UpdatedAt, out var dt)
                    ? dt
                    : DateTimeOffset.UtcNow,
                Metadata = item.Metadata.ToDictionary(k => k.Key, v => v.Value)
            }).ToList();

            var facets = response.FacetCounts.ToDictionary(k => k.Key, v => (int)v.Value);

            return new SearchResultDto
            {
                Items = items,
                TotalCount = response.TotalCount,
                Page = response.Page,
                PageSize = response.PageSize,
                FacetCounts = facets
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogDebug("Search module gRPC service unavailable at {Address}", _options.SearchModuleAddress);
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning("Search gRPC call timed out after {Timeout}", _options.Timeout);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Search module gRPC");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RequestModuleReindexAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (!IsAvailable)
        {
            _logger.LogDebug("Search FTS client unavailable — Search module address not configured");
            return false;
        }

        try
        {
            var response = await _client.Value.ReindexModuleAsync(
                new ReindexModuleRequest
                {
                    ModuleId = moduleId,
                },
                CreateCallOptions(cancellationToken));

            if (!response.Success)
            {
                _logger.LogWarning("Search module reindex request failed for {ModuleId}: {Error}", moduleId, response.ErrorMessage);
                return false;
            }

            return true;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogDebug("Search module gRPC service unavailable at {Address}", _options.SearchModuleAddress);
            return false;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning("Search reindex request for {ModuleId} timed out after {Timeout}", moduleId, _options.Timeout);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error requesting Search module reindex for {ModuleId}", moduleId);
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_channel.IsValueCreated)
        {
            _channel.Value.Dispose();
        }
    }

    private GrpcChannel CreateChannel()
    {
        var channelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 16 * 1024 * 1024,
            MaxSendMessageSize = 16 * 1024 * 1024,
        };

        var address = _options.SearchModuleAddress!;

        // Support Unix socket addresses
        if (address.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
        {
            var socketPath = address["unix://".Length..];
            channelOptions.HttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = async (_, cancellationToken) =>
                {
                    var socket = new System.Net.Sockets.Socket(
                        System.Net.Sockets.AddressFamily.Unix,
                        System.Net.Sockets.SocketType.Stream,
                        System.Net.Sockets.ProtocolType.Unspecified);
                    var endPoint = new System.Net.Sockets.UnixDomainSocketEndPoint(socketPath);
                    await socket.ConnectAsync(endPoint, cancellationToken);
                    return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
                }
            };
            address = "http://localhost";
        }

        return GrpcChannel.ForAddress(address, channelOptions);
    }

    private CallOptions CreateCallOptions(CancellationToken cancellationToken)
    {
        return new CallOptions(
            deadline: DateTime.UtcNow.Add(_options.Timeout),
            cancellationToken: cancellationToken);
    }
}
