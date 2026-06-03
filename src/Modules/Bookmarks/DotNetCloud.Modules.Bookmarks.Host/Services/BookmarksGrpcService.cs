using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Bookmarks.Host.Protos;
using DotNetCloud.Modules.Bookmarks.Models;
using Grpc.Core;

namespace DotNetCloud.Modules.Bookmarks.Host.Services;

/// <summary>
/// gRPC service implementation for the Bookmarks module.
/// </summary>
public sealed class BookmarksGrpcService : BookmarksService.BookmarksServiceBase
{
    private readonly DotNetCloud.Modules.Bookmarks.Services.IBookmarkService _bookmarkService;
    private readonly DotNetCloud.Modules.Bookmarks.Services.IBookmarkFolderService _folderService;
    private readonly DotNetCloud.Modules.Bookmarks.Services.IBookmarkPreviewService _previewService;
    private readonly ILogger<BookmarksGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarksGrpcService"/> class.
    /// </summary>
    public BookmarksGrpcService(
        DotNetCloud.Modules.Bookmarks.Services.IBookmarkService bookmarkService,
        DotNetCloud.Modules.Bookmarks.Services.IBookmarkFolderService folderService,
        DotNetCloud.Modules.Bookmarks.Services.IBookmarkPreviewService previewService,
        ILogger<BookmarksGrpcService> logger)
    {
        _bookmarkService = bookmarkService;
        _folderService = folderService;
        _previewService = previewService;
        _logger = logger;
    }

    // ─── Bookmarks ──────────────────────────────────────────────────────────

    public override async Task<ListBookmarksResponse> ListBookmarks(
        ListBookmarksRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new ListBookmarksResponse { Success = false, ErrorMessage = "Invalid user ID." };

        Guid? folderId = Guid.TryParse(request.FolderId, out var fid) ? fid : null;
        var take = request.Take > 0 ? request.Take : 50;

        try
        {
            var results = await _bookmarkService.ListAsync(
                new CallerContext(userId, ["user"], CallerType.User),
                folderId, request.Skip, take, context.CancellationToken);

            var response = new ListBookmarksResponse { Success = true };
            response.Bookmarks.AddRange(results.Select(ToMessage));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListBookmarks gRPC failed");
            return new ListBookmarksResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<BookmarkResponse> GetBookmark(
        GetBookmarkRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.BookmarkId, out var id) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new BookmarkResponse { Success = false, ErrorMessage = "Invalid ID." };

        var result = await _bookmarkService.GetAsync(
            id, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

        return result is null
            ? new BookmarkResponse { Success = false, ErrorMessage = "Not found." }
            : new BookmarkResponse { Success = true, Bookmark = ToMessage(result) };
    }

    public override async Task<BookmarkResponse> CreateBookmark(
        CreateBookmarkRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new BookmarkResponse { Success = false, ErrorMessage = "Invalid user ID." };

        var dto = new DotNetCloud.Modules.Bookmarks.Services.CreateBookmarkRequest
        {
            Url = request.Url,
            Title = NullIfEmpty(request.Title),
            Description = NullIfEmpty(request.Description),
            Notes = NullIfEmpty(request.Notes),
            Tags = request.Tags?.ToList(),
            FolderId = Guid.TryParse(request.FolderId, out var fid) ? fid : null
        };

        try
        {
            var result = await _bookmarkService.CreateAsync(
                dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new BookmarkResponse { Success = true, Bookmark = ToMessage(result) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateBookmark gRPC failed");
            return new BookmarkResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<BookmarkResponse> UpdateBookmark(
        UpdateBookmarkRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.BookmarkId, out var id) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new BookmarkResponse { Success = false, ErrorMessage = "Invalid ID." };

        var dto = new DotNetCloud.Modules.Bookmarks.Services.UpdateBookmarkRequest
        {
            Url = NullIfEmpty(request.Url),
            Title = NullIfEmpty(request.Title),
            Description = NullIfEmpty(request.Description),
            Notes = NullIfEmpty(request.Notes),
            Tags = request.UpdateTags ? request.Tags?.ToList() : null,
            FolderId = Guid.TryParse(request.FolderId, out var fid) ? fid : null
        };

        try
        {
            var result = await _bookmarkService.UpdateAsync(
                id, dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new BookmarkResponse { Success = true, Bookmark = ToMessage(result) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateBookmark gRPC failed");
            return new BookmarkResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<DeleteBookmarkResponse> DeleteBookmark(
        DeleteBookmarkRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.BookmarkId, out var id) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new DeleteBookmarkResponse { Success = false, ErrorMessage = "Invalid ID." };

        try
        {
            await _bookmarkService.DeleteAsync(
                id, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new DeleteBookmarkResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteBookmark gRPC failed");
            return new DeleteBookmarkResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<ListBookmarksResponse> SearchBookmarks(
        SearchBookmarksRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new ListBookmarksResponse { Success = false, ErrorMessage = "Invalid user ID." };

        var take = request.Take > 0 ? request.Take : 50;

        try
        {
            var results = await _bookmarkService.SearchAsync(
                new CallerContext(userId, ["user"], CallerType.User),
                request.Query, request.Skip, take, context.CancellationToken);

            var response = new ListBookmarksResponse { Success = true };
            response.Bookmarks.AddRange(results.Select(ToMessage));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchBookmarks gRPC failed");
            return new ListBookmarksResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ─── Folders ────────────────────────────────────────────────────────────

    public override async Task<ListBookmarkFoldersResponse> ListFolders(
        ListBookmarkFoldersRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new ListBookmarkFoldersResponse { Success = false };

        Guid? parentId = Guid.TryParse(request.ParentId, out var pid) ? pid : null;

        try
        {
            var results = await _folderService.ListAsync(
                new CallerContext(userId, ["user"], CallerType.User), parentId, context.CancellationToken);

            var response = new ListBookmarkFoldersResponse { Success = true };
            response.Folders.AddRange(results.Select(ToFolderMessage));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListBookmarkFolders gRPC failed");
            return new ListBookmarkFoldersResponse { Success = false };
        }
    }

    public override async Task<BookmarkFolderResponse> GetFolder(
        GetBookmarkFolderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.FolderId, out var id) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new BookmarkFolderResponse { Success = false, ErrorMessage = "Invalid ID." };

        var result = await _folderService.GetAsync(
            id, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

        return result is null
            ? new BookmarkFolderResponse { Success = false, ErrorMessage = "Not found." }
            : new BookmarkFolderResponse { Success = true, Folder = ToFolderMessage(result) };
    }

    public override async Task<BookmarkFolderResponse> CreateFolder(
        CreateBookmarkFolderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new BookmarkFolderResponse { Success = false, ErrorMessage = "Invalid user ID." };

        var dto = new DotNetCloud.Modules.Bookmarks.Services.CreateBookmarkFolderRequest
        {
            Name = request.Name,
            ParentId = Guid.TryParse(request.ParentId, out var pid) ? pid : null,
            Color = NullIfEmpty(request.Color)
        };

        try
        {
            var result = await _folderService.CreateAsync(
                dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new BookmarkFolderResponse { Success = true, Folder = ToFolderMessage(result) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateBookmarkFolder gRPC failed");
            return new BookmarkFolderResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<BookmarkFolderResponse> UpdateFolder(
        UpdateBookmarkFolderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.FolderId, out var id) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new BookmarkFolderResponse { Success = false, ErrorMessage = "Invalid ID." };

        var dto = new DotNetCloud.Modules.Bookmarks.Services.UpdateBookmarkFolderRequest
        {
            Name = NullIfEmpty(request.Name),
            Color = NullIfEmpty(request.Color)
        };

        try
        {
            var result = await _folderService.UpdateAsync(
                id, dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new BookmarkFolderResponse { Success = true, Folder = ToFolderMessage(result) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateBookmarkFolder gRPC failed");
            return new BookmarkFolderResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<DeleteBookmarkFolderResponse> DeleteFolder(
        DeleteBookmarkFolderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.FolderId, out var id) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new DeleteBookmarkFolderResponse { Success = false, ErrorMessage = "Invalid ID." };

        try
        {
            await _folderService.DeleteAsync(
                id, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new DeleteBookmarkFolderResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteBookmarkFolder gRPC failed");
            return new DeleteBookmarkFolderResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ─── Previews ───────────────────────────────────────────────────────────

    public override async Task<PreviewResponse> FetchPreview(
        FetchPreviewRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.BookmarkId, out var id))
            return new PreviewResponse { Success = false };

        try
        {
            var preview = await _previewService.FetchPreviewAsync(id, context.CancellationToken);
            return new PreviewResponse { Success = true, Preview = ToPreviewMessage(preview) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FetchPreview gRPC failed");
            return new PreviewResponse { Success = false };
        }
    }

    public override async Task<PreviewResponse> GetPreview(
        GetPreviewRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.BookmarkId, out var id))
            return new PreviewResponse { Success = false };

        try
        {
            var preview = await _previewService.GetPreviewAsync(id, context.CancellationToken);
            if (preview is null)
                return new PreviewResponse { Success = false };

            return new PreviewResponse { Success = true, Preview = ToPreviewMessage(preview) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPreview gRPC failed");
            return new PreviewResponse { Success = false };
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;

    private static BookmarkMessage ToMessage(BookmarkItem item) => new()
    {
        Id = item.Id.ToString(),
        OwnerId = item.OwnerId.ToString(),
        Url = item.Url,
        Title = item.Title ?? string.Empty,
        Description = item.Description ?? string.Empty,
        Notes = item.Notes ?? string.Empty,
        FolderId = item.FolderId?.ToString() ?? string.Empty,
        CreatedAt = item.CreatedAt.ToString("O"),
        UpdatedAt = item.UpdatedAt.ToString("O")
    };

    private static BookmarkFolderMessage ToFolderMessage(BookmarkFolder folder) => new()
    {
        Id = folder.Id.ToString(),
        OwnerId = folder.OwnerId.ToString(),
        Name = folder.Name,
        ParentId = folder.ParentId?.ToString() ?? string.Empty,
        Color = folder.Color ?? string.Empty,
        SortOrder = folder.SortOrder,
        CreatedAt = folder.CreatedAt.ToString("O"),
        UpdatedAt = folder.UpdatedAt.ToString("O")
    };

    private static BookmarkPreviewMessage ToPreviewMessage(BookmarkPreview preview) => new()
    {
        Id = preview.Id.ToString(),
        BookmarkId = preview.BookmarkId.ToString(),
        Title = preview.ResolvedTitle ?? string.Empty,
        Description = preview.ResolvedDescription ?? string.Empty,
        ImageUrl = preview.PreviewImageUrl ?? string.Empty,
        FaviconUrl = preview.FaviconUrl ?? string.Empty,
        SiteName = preview.SiteName ?? string.Empty,
        FetchedAt = preview.FetchedAt?.ToString("O") ?? string.Empty
    };
}
