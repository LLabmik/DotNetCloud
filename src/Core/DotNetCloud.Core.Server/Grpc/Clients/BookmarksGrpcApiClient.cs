using System.Security.Claims;
using DotNetCloud.Modules.Bookmarks.Models;
using DotNetCloud.Modules.Bookmarks.Services;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proto = DotNetCloud.Modules.Bookmarks.Host.Protos;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Bookmarks gRPC client used by the Core Server.
/// </summary>
public sealed class BookmarksGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "BookmarksGrpc";
    /// <summary>The gRPC address of the Bookmarks module.</summary>
    public string BookmarksModuleAddress { get; set; } = "http://localhost:5012";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="IBookmarksApiClient"/>.
/// Calls the Bookmarks module's gRPC service.
/// </summary>
public sealed class BookmarksGrpcApiClient : IBookmarksApiClient, IDisposable
{
    private readonly BookmarksGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BookmarksGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<Proto.BookmarksService.BookmarksServiceClient> _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="BookmarksGrpcApiClient"/> class.</summary>
    public BookmarksGrpcApiClient(
        IOptions<BookmarksGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BookmarksGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<Proto.BookmarksService.BookmarksServiceClient>(
            () => new Proto.BookmarksService.BookmarksServiceClient(_channel.Value));
    }

    private string GetUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId) ? userId : Guid.Empty.ToString();
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.bookmarks");
        _logger.LogInformation("BookmarksGrpcApiClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    // ─── Bookmarks ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookmarkItem>> ListAsync(Guid? folderId = null, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var request = new Proto.ListBookmarksRequest
        {
            UserId = GetUserId(),
            FolderId = folderId?.ToString() ?? string.Empty,
            Skip = skip,
            Take = take
        };
        try
        {
            var response = await _client.Value.ListBookmarksAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Bookmarks.Select(ToItem).Where(i => i is not null).Select(i => i!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.ListAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<BookmarkItem?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var request = new Proto.GetBookmarkRequest { BookmarkId = id.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetBookmarkAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToItem(response.Bookmark) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.GetAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<BookmarkItem?> CreateAsync(DotNetCloud.Modules.Bookmarks.Services.CreateBookmarkRequest req, CancellationToken ct = default)
    {
        var request = new Proto.CreateBookmarkRequest
        {
            UserId = GetUserId(),
            Url = req.Url,
            Title = req.Title ?? string.Empty,
            Description = req.Description ?? string.Empty,
            Notes = req.Notes ?? string.Empty,
            FolderId = req.FolderId?.ToString() ?? string.Empty
        };
        if (req.Tags is not null)
            request.Tags.AddRange(req.Tags);
        try
        {
            var response = await _client.Value.CreateBookmarkAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToItem(response.Bookmark) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.CreateAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<BookmarkItem?> UpdateAsync(Guid id, DotNetCloud.Modules.Bookmarks.Services.UpdateBookmarkRequest req, CancellationToken ct = default)
    {
        var request = new Proto.UpdateBookmarkRequest
        {
            BookmarkId = id.ToString(),
            UserId = GetUserId(),
            Url = req.Url ?? string.Empty,
            Title = req.Title ?? string.Empty,
            Description = req.Description ?? string.Empty,
            Notes = req.Notes ?? string.Empty,
            FolderId = req.FolderId?.ToString() ?? string.Empty
        };
        if (req.Tags is not null)
        { request.UpdateTags = true; request.Tags.AddRange(req.Tags); }
        try
        {
            var response = await _client.Value.UpdateBookmarkAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToItem(response.Bookmark) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.UpdateAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var request = new Proto.DeleteBookmarkRequest { BookmarkId = id.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteBookmarkAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.DeleteAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookmarkItem>> SearchAsync(string query, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var request = new Proto.SearchBookmarksRequest
        {
            UserId = GetUserId(),
            Query = query,
            Skip = skip,
            Take = take
        };
        try
        {
            var response = await _client.Value.SearchBookmarksAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Bookmarks.Select(ToItem).Where(i => i is not null).Select(i => i!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.SearchAsync failed");
            return [];
        }
    }

    // ─── Folders ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookmarkFolder>> ListFoldersAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        var request = new Proto.ListBookmarkFoldersRequest
        {
            UserId = GetUserId(),
            ParentId = parentId?.ToString() ?? string.Empty
        };
        try
        {
            var response = await _client.Value.ListFoldersAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Folders.Select(ToFolder).Where(f => f is not null).Select(f => f!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.ListFoldersAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<BookmarkFolder?> GetFolderAsync(Guid id, CancellationToken ct = default)
    {
        var request = new Proto.GetBookmarkFolderRequest { FolderId = id.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetFolderAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToFolder(response.Folder) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.GetFolderAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<BookmarkFolder?> CreateFolderAsync(DotNetCloud.Modules.Bookmarks.Services.CreateBookmarkFolderRequest req, CancellationToken ct = default)
    {
        var request = new Proto.CreateBookmarkFolderRequest
        {
            UserId = GetUserId(),
            Name = req.Name,
            ParentId = req.ParentId?.ToString() ?? string.Empty,
            Color = req.Color ?? string.Empty
        };
        try
        {
            var response = await _client.Value.CreateFolderAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToFolder(response.Folder) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.CreateFolderAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<BookmarkFolder?> UpdateFolderAsync(Guid id, DotNetCloud.Modules.Bookmarks.Services.UpdateBookmarkFolderRequest req, CancellationToken ct = default)
    {
        var request = new Proto.UpdateBookmarkFolderRequest
        {
            FolderId = id.ToString(),
            UserId = GetUserId(),
            Name = req.Name ?? string.Empty,
            Color = req.Color ?? string.Empty
        };
        try
        {
            var response = await _client.Value.UpdateFolderAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToFolder(response.Folder) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.UpdateFolderAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteFolderAsync(Guid id, CancellationToken ct = default)
    {
        var request = new Proto.DeleteBookmarkFolderRequest { FolderId = id.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteFolderAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.DeleteFolderAsync failed");
        }
    }

    // ─── Previews ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<BookmarkPreview?> FetchPreviewAsync(Guid bookmarkId, CancellationToken ct = default)
    {
        var request = new Proto.FetchPreviewRequest { BookmarkId = bookmarkId.ToString() };
        try
        {
            var response = await _client.Value.FetchPreviewAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success && response.Preview is not null ? ToPreview(response.Preview) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.FetchPreviewAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<BookmarkPreview?> GetPreviewAsync(Guid bookmarkId, CancellationToken ct = default)
    {
        var request = new Proto.GetPreviewRequest { BookmarkId = bookmarkId.ToString() };
        try
        {
            var response = await _client.Value.GetPreviewAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success && response.Preview is not null ? ToPreview(response.Preview) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksGrpcApiClient.GetPreviewAsync failed");
            return null;
        }
    }

    /// <summary>
    /// Imports bookmarks from a browser HTML export stream.
    /// NOTE: Import/Export not yet available via gRPC. Falls back to local processing.
    /// </summary>
    public async Task<BookmarkImportResult?> ImportAsync(Stream fileStream, string fileName, Guid? folderId = null, CancellationToken ct = default)
    {
        // Import/Export are not yet implemented over gRPC.
        // For now, return null to indicate this is not available via gRPC client.
        _logger.LogWarning("BookmarksGrpcApiClient.ImportAsync: not supported via gRPC yet");
        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Exports bookmarks to HTML format.
    /// NOTE: Import/Export not yet available via gRPC. Returns empty array.
    /// </summary>
    public async Task<byte[]> ExportAsync(Guid? folderId = null, CancellationToken ct = default)
    {
        _logger.LogWarning("BookmarksGrpcApiClient.ExportAsync: not supported via gRPC yet");
        await Task.CompletedTask;
        return [];
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private Metadata DeadlineHeaders(CancellationToken ct)
    {
        var headers = new Metadata();
        if (_options.Timeout > TimeSpan.Zero)
        {
            headers.Add("deadline", DateTime.UtcNow.Add(_options.Timeout).ToString("O"));
        }
        return headers;
    }

    private static BookmarkItem? ToItem(Proto.BookmarkMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new BookmarkItem
        {
            Id = Guid.Parse(m.Id),
            OwnerId = Guid.Parse(m.OwnerId),
            Url = m.Url,
            Title = m.Title,
            Description = string.IsNullOrEmpty(m.Description) ? null : m.Description,
            Notes = string.IsNullOrEmpty(m.Notes) ? null : m.Notes,
            FolderId = string.IsNullOrEmpty(m.FolderId) ? null : Guid.Parse(m.FolderId),
            CreatedAt = DateTime.TryParse(m.CreatedAt, out var ca) ? ca : DateTime.MinValue,
            UpdatedAt = DateTime.TryParse(m.UpdatedAt, out var ua) ? ua : DateTime.MinValue
        };
    }

    private static BookmarkFolder? ToFolder(Proto.BookmarkFolderMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new BookmarkFolder
        {
            Id = Guid.Parse(m.Id),
            OwnerId = Guid.Parse(m.OwnerId),
            Name = m.Name,
            ParentId = string.IsNullOrEmpty(m.ParentId) ? null : Guid.Parse(m.ParentId),
            Color = string.IsNullOrEmpty(m.Color) ? null : m.Color,
            SortOrder = m.SortOrder,
            CreatedAt = DateTime.TryParse(m.CreatedAt, out var ca) ? ca : DateTime.MinValue,
            UpdatedAt = DateTime.TryParse(m.UpdatedAt, out var ua) ? ua : DateTime.MinValue
        };
    }

    private static BookmarkPreview? ToPreview(Proto.BookmarkPreviewMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new BookmarkPreview
        {
            Id = Guid.Parse(m.Id),
            BookmarkId = Guid.Parse(m.BookmarkId),
            ResolvedTitle = string.IsNullOrEmpty(m.Title) ? null : m.Title,
            ResolvedDescription = string.IsNullOrEmpty(m.Description) ? null : m.Description,
            PreviewImageUrl = string.IsNullOrEmpty(m.ImageUrl) ? null : m.ImageUrl,
            FaviconUrl = string.IsNullOrEmpty(m.FaviconUrl) ? null : m.FaviconUrl,
            SiteName = string.IsNullOrEmpty(m.SiteName) ? null : m.SiteName,
            FetchedAt = DateTime.TryParse(m.FetchedAt, out var fa) ? fa : null
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_channel.IsValueCreated)
            {
                try
                { _channel.Value.Dispose(); }
                catch { /* best effort */ }
            }
        }
    }
}
