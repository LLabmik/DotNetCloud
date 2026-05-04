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

    /// <inheritdoc />
    public async Task<BookmarkSyncChangesResult> GetSyncChangesAsync(DateTimeOffset since, int limit, CallerContext caller, CancellationToken ct = default)
    {
        var utcSince = since.UtcDateTime;
        var take = Math.Min(limit, 500);

        // Bookmarks that were created or updated
        var changedBookmarks = await _db.Bookmarks.AsNoTracking()
            .Where(b => b.OwnerId == caller.UserId && b.UpdatedAt > utcSince && !b.IsDeleted)
            .OrderBy(b => b.UpdatedAt)
            .Take(take)
            .ToListAsync(ct);

        // Bookmark IDs that were soft-deleted
        var deletedBookmarkIds = await _db.Bookmarks.AsNoTracking()
            .Where(b => b.OwnerId == caller.UserId && b.DeletedAt != null && b.DeletedAt > utcSince)
            .OrderBy(b => b.DeletedAt)
            .Select(b => b.Id)
            .Take(take)
            .ToListAsync(ct);

        // Folders that were created or updated
        var changedFolders = await _db.BookmarkFolders.AsNoTracking()
            .Where(f => f.OwnerId == caller.UserId && f.UpdatedAt > utcSince && !f.IsDeleted)
            .OrderBy(f => f.UpdatedAt)
            .Take(take)
            .ToListAsync(ct);

        // Folder IDs that were soft-deleted
        var deletedFolderIds = await _db.BookmarkFolders.AsNoTracking()
            .Where(f => f.OwnerId == caller.UserId && f.DeletedAt != null && f.DeletedAt > utcSince)
            .OrderBy(f => f.DeletedAt)
            .Select(f => f.Id)
            .Take(take)
            .ToListAsync(ct);

        var nextCursor = DateTime.UtcNow;

        var hasMore = changedBookmarks.Count == take
            || deletedBookmarkIds.Count == take
            || changedFolders.Count == take
            || deletedFolderIds.Count == take;

        return new BookmarkSyncChangesResult
        {
            Items = changedBookmarks.Select(b => new BookmarkSyncItem
            {
                Id = b.Id,
                FolderId = b.FolderId,
                Url = b.Url,
                Title = b.Title,
                Description = b.Description,
                Tags = b.TagsJson is not null
                    ? System.Text.Json.JsonSerializer.Deserialize<IReadOnlyList<string>>(b.TagsJson) ?? []
                    : [],
                Notes = b.Notes,
                UpdatedAt = b.UpdatedAt,
            }).ToList(),
            DeletedIds = deletedBookmarkIds,
            Folders = changedFolders.Select(f => new BookmarkSyncFolderItem
            {
                Id = f.Id,
                ParentId = f.ParentId,
                Name = f.Name,
                UpdatedAt = f.UpdatedAt,
            }).ToList(),
            DeletedFolderIds = deletedFolderIds,
            NextCursor = nextCursor,
            HasMore = hasMore,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BatchOperationResult>> BatchAsync(BatchRequest request, CallerContext caller, CancellationToken ct = default)
    {
        var totalOps = (request.Creates?.Count ?? 0)
                     + (request.Updates?.Count ?? 0)
                     + (request.Deletes?.Count ?? 0)
                     + (request.FolderCreates?.Count ?? 0)
                     + (request.FolderDeletes?.Count ?? 0);

        if (totalOps > 500)
            throw new ValidationException(ErrorCodes.ValidationError, "Batch request exceeds maximum of 500 operations.");

        var results = new List<BatchOperationResult>();

        try
        {
            // Process folder creates first
            if (request.FolderCreates is { Count: > 0 })
            {
                foreach (var fc in request.FolderCreates)
                {
                    try
                    {
                        var folder = new BookmarkFolder
                        {
                            OwnerId = caller.UserId,
                            Name = fc.Name,
                            ParentId = fc.ParentId,
                        };
                        _db.BookmarkFolders.Add(folder);
                        await _db.SaveChangesAsync(ct);
                        results.Add(new BatchOperationResult
                        {
                            Operation = "folderCreate",
                            ClientRef = fc.Name,
                            ServerId = folder.Id,
                            Success = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Batch folder create failed for '{Name}'", fc.Name);
                        results.Add(new BatchOperationResult
                        {
                            Operation = "folderCreate",
                            ClientRef = fc.Name,
                            Success = false,
                            Error = ex.Message,
                        });
                    }
                }
            }

            // Process bookmark creates
            if (request.Creates is { Count: > 0 })
            {
                foreach (var bc in request.Creates)
                {
                    try
                    {
                        var bookmark = new BookmarkItem
                        {
                            OwnerId = caller.UserId,
                            Url = bc.Url,
                            NormalizedUrl = NormalizeUrl(bc.Url),
                            Title = bc.Title ?? string.Empty,
                            Description = bc.Description,
                            Notes = bc.Notes,
                            TagsJson = bc.Tags is { Count: > 0 }
                                ? System.Text.Json.JsonSerializer.Serialize(bc.Tags)
                                : null,
                            FolderId = bc.FolderId,
                            IsFavorite = bc.IsFavorite ?? false,
                        };
                        _db.Bookmarks.Add(bookmark);
                        await _db.SaveChangesAsync(ct);
                        results.Add(new BatchOperationResult
                        {
                            Operation = "create",
                            ClientRef = bc.ClientRef,
                            ServerId = bookmark.Id,
                            Success = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Batch bookmark create failed");
                        results.Add(new BatchOperationResult
                        {
                            Operation = "create",
                            ClientRef = bc.ClientRef,
                            Success = false,
                            Error = ex.Message,
                        });
                    }
                }
            }

            // Process bookmark updates
            if (request.Updates is { Count: > 0 })
            {
                foreach (var bu in request.Updates)
                {
                    try
                    {
                        var bookmark = await _db.Bookmarks
                            .FirstOrDefaultAsync(b => b.Id == bu.Id && b.OwnerId == caller.UserId, ct);

                        if (bookmark is null)
                        {
                            results.Add(new BatchOperationResult
                            {
                                Operation = "update",
                                Id = bu.Id,
                                Success = false,
                                Error = "not_found",
                            });
                            continue;
                        }

                        if (bu.Url is not null)
                        {
                            bookmark.Url = bu.Url;
                            bookmark.NormalizedUrl = NormalizeUrl(bu.Url);
                        }
                        if (bu.Title is not null) bookmark.Title = bu.Title;
                        if (bu.Description is not null) bookmark.Description = bu.Description;
                        if (bu.Notes is not null) bookmark.Notes = bu.Notes;
                        if (bu.Tags is not null)
                            bookmark.TagsJson = bu.Tags.Count > 0
                                ? System.Text.Json.JsonSerializer.Serialize(bu.Tags)
                                : null;
                        if (bu.FolderId is not null) bookmark.FolderId = bu.FolderId;
                        if (bu.IsFavorite.HasValue) bookmark.IsFavorite = bu.IsFavorite.Value;
                        bookmark.UpdatedAt = DateTime.UtcNow;

                        await _db.SaveChangesAsync(ct);
                        results.Add(new BatchOperationResult
                        {
                            Operation = "update",
                            Id = bu.Id,
                            Success = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Batch bookmark update failed for {Id}", bu.Id);
                        results.Add(new BatchOperationResult
                        {
                            Operation = "update",
                            Id = bu.Id,
                            Success = false,
                            Error = ex.Message,
                        });
                    }
                }
            }

            // Process bookmark deletes
            if (request.Deletes is { Count: > 0 })
            {
                foreach (var id in request.Deletes)
                {
                    try
                    {
                        var bookmark = await _db.Bookmarks
                            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == caller.UserId, ct);

                        if (bookmark is null)
                        {
                            results.Add(new BatchOperationResult
                            {
                                Operation = "delete",
                                Id = id,
                                Success = false,
                                Error = "not_found",
                            });
                            continue;
                        }

                        bookmark.IsDeleted = true;
                        bookmark.DeletedAt = DateTime.UtcNow;
                        bookmark.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(ct);
                        results.Add(new BatchOperationResult
                        {
                            Operation = "delete",
                            Id = id,
                            Success = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Batch bookmark delete failed for {Id}", id);
                        results.Add(new BatchOperationResult
                        {
                            Operation = "delete",
                            Id = id,
                            Success = false,
                            Error = ex.Message,
                        });
                    }
                }
            }

            // Process folder deletes
            if (request.FolderDeletes is { Count: > 0 })
            {
                foreach (var id in request.FolderDeletes)
                {
                    try
                    {
                        var folder = await _db.BookmarkFolders
                            .Include(f => f.Bookmarks)
                            .Include(f => f.Children)
                            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == caller.UserId, ct);

                        if (folder is null)
                        {
                            results.Add(new BatchOperationResult
                            {
                                Operation = "folderDelete",
                                Id = id,
                                Success = false,
                                Error = "not_found",
                            });
                            continue;
                        }

                        var now = DateTime.UtcNow;
                        folder.IsDeleted = true;
                        folder.DeletedAt = now;
                        folder.UpdatedAt = now;

                        foreach (var bookmark in folder.Bookmarks)
                        {
                            bookmark.IsDeleted = true;
                            bookmark.DeletedAt = now;
                            bookmark.FolderId = null;
                        }

                        foreach (var child in folder.Children)
                        {
                            child.ParentId = folder.ParentId;
                        }

                        await _db.SaveChangesAsync(ct);
                        results.Add(new BatchOperationResult
                        {
                            Operation = "folderDelete",
                            Id = id,
                            Success = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Batch folder delete failed for {Id}", id);
                        results.Add(new BatchOperationResult
                        {
                            Operation = "folderDelete",
                            Id = id,
                            Success = false,
                            Error = ex.Message,
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch operation infrastructure error — rolling back");
            throw;
        }

        return results;
    }

    private static string NormalizeUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath + uri.Query;
        return url.ToLowerInvariant().TrimEnd('/');
    }
}
