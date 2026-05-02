using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Bookmarks.Models;
using DotNetCloud.Modules.Bookmarks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks.Data.Services;

/// <summary>
/// Service implementation for managing bookmarks.
/// </summary>
public sealed class BookmarkService : IBookmarkService
{
    private readonly BookmarksDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<BookmarkService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarkService"/> class.
    /// </summary>
    public BookmarkService(BookmarksDbContext db, IEventBus eventBus, ILogger<BookmarkService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookmarkItem>> ListAsync(CallerContext caller, Guid? folderId, int skip, int take, CancellationToken ct = default)
    {
        var query = _db.Bookmarks.AsNoTracking()
            .Where(b => b.OwnerId == caller.UserId);

        if (folderId.HasValue)
            query = query.Where(b => b.FolderId == folderId.Value);

        return await query.OrderByDescending(b => b.UpdatedAt)
            .Skip(skip).Take(take).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BookmarkItem?> GetAsync(Guid id, CallerContext caller, CancellationToken ct = default)
    {
        return await _db.Bookmarks.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == caller.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<BookmarkItem> CreateAsync(CreateBookmarkRequest request, CallerContext caller, CancellationToken ct = default)
    {
        var normalizedUrl = NormalizeUrl(request.Url);
        var bookmark = new BookmarkItem
        {
            OwnerId = caller.UserId,
            Url = request.Url,
            NormalizedUrl = normalizedUrl,
            Title = request.Title ?? string.Empty,
            Description = request.Description,
            Notes = request.Notes,
            TagsJson = request.Tags is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.Tags) : null,
            FolderId = request.FolderId
        };

        _db.Bookmarks.Add(bookmark);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Bookmark created: {BookmarkId} '{Title}'", bookmark.Id, bookmark.Title);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "bookmarks",
            EntityId = bookmark.Id.ToString(),
            Action = SearchIndexAction.Index
        }, caller, ct);

        return bookmark;
    }

    /// <inheritdoc />
    public async Task<BookmarkItem> UpdateAsync(Guid id, UpdateBookmarkRequest request, CallerContext caller, CancellationToken ct = default)
    {
        var bookmark = await _db.Bookmarks
            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == caller.UserId, ct)
            ?? throw new ValidationException(ErrorCodes.BookmarkNotFound, "Bookmark not found.");

        if (request.Url is not null)
        {
            bookmark.Url = request.Url;
            bookmark.NormalizedUrl = NormalizeUrl(request.Url);
        }
        if (request.Title is not null) bookmark.Title = request.Title;
        if (request.Description is not null) bookmark.Description = request.Description;
        if (request.Notes is not null) bookmark.Notes = request.Notes;
        if (request.Tags is not null)
            bookmark.TagsJson = request.Tags.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(request.Tags) : null;
        if (request.FolderId is not null) bookmark.FolderId = request.FolderId;
        if (request.IsFavorite.HasValue) bookmark.IsFavorite = request.IsFavorite.Value;
        bookmark.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Bookmark updated: {BookmarkId}", bookmark.Id);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "bookmarks",
            EntityId = bookmark.Id.ToString(),
            Action = SearchIndexAction.Index
        }, caller, ct);

        return bookmark;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CallerContext caller, CancellationToken ct = default)
    {
        var bookmark = await _db.Bookmarks
            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == caller.UserId, ct)
            ?? throw new ValidationException(ErrorCodes.BookmarkNotFound, "Bookmark not found.");

        bookmark.IsDeleted = true;
        bookmark.DeletedAt = DateTime.UtcNow;
        bookmark.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Bookmark deleted: {BookmarkId}", id);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "bookmarks",
            EntityId = id.ToString(),
            Action = SearchIndexAction.Remove
        }, caller, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookmarkItem>> SearchAsync(CallerContext caller, string query, int skip, int take, CancellationToken ct = default)
    {
        return await _db.Bookmarks.AsNoTracking()
            .Where(b => b.OwnerId == caller.UserId &&
                        (b.Title.Contains(query) ||
                         b.Description!.Contains(query) ||
                         b.Notes!.Contains(query) ||
                         b.Url.Contains(query)))
            .OrderByDescending(b => b.UpdatedAt)
            .Skip(skip).Take(take).ToListAsync(ct);
    }

    private static string NormalizeUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath + uri.Query;
        return url.ToLowerInvariant().TrimEnd('/');
    }
}
