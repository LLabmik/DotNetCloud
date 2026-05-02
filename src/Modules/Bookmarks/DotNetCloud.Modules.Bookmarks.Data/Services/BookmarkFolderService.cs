using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Bookmarks.Models;
using DotNetCloud.Modules.Bookmarks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks.Data.Services;

/// <summary>
/// Service implementation for managing bookmark folders.
/// </summary>
public sealed class BookmarkFolderService : IBookmarkFolderService
{
    private readonly BookmarksDbContext _db;
    private readonly ILogger<BookmarkFolderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarkFolderService"/> class.
    /// </summary>
    public BookmarkFolderService(BookmarksDbContext db, ILogger<BookmarkFolderService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookmarkFolder>> ListAsync(CallerContext caller, Guid? parentId, CancellationToken ct = default)
    {
        return await _db.BookmarkFolders.AsNoTracking()
            .Where(f => f.OwnerId == caller.UserId && f.ParentId == parentId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BookmarkFolder?> GetAsync(Guid id, CallerContext caller, CancellationToken ct = default)
    {
        return await _db.BookmarkFolders.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == caller.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<BookmarkFolder> CreateAsync(CreateBookmarkFolderRequest request, CallerContext caller, CancellationToken ct = default)
    {
        var folder = new BookmarkFolder
        {
            OwnerId = caller.UserId,
            Name = request.Name,
            ParentId = request.ParentId,
            Color = request.Color
        };

        _db.BookmarkFolders.Add(folder);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Bookmark folder created: {FolderId} '{Name}'", folder.Id, folder.Name);
        return folder;
    }

    /// <inheritdoc />
    public async Task<BookmarkFolder> UpdateAsync(Guid id, UpdateBookmarkFolderRequest request, CallerContext caller, CancellationToken ct = default)
    {
        var folder = await _db.BookmarkFolders
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == caller.UserId, ct)
            ?? throw new ValidationException(ErrorCodes.BookmarkFolderNotFound, "Bookmark folder not found.");

        if (request.Name is not null) folder.Name = request.Name;
        if (request.ParentId is not null) folder.ParentId = request.ParentId;
        if (request.Color is not null) folder.Color = request.Color;
        if (request.SortOrder.HasValue) folder.SortOrder = request.SortOrder.Value;
        folder.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Bookmark folder updated: {FolderId}", id);
        return folder;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CallerContext caller, CancellationToken ct = default)
    {
        var folder = await _db.BookmarkFolders
            .Include(f => f.Bookmarks)
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == caller.UserId, ct)
            ?? throw new ValidationException(ErrorCodes.BookmarkFolderNotFound, "Bookmark folder not found.");

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
        _logger.LogInformation("Bookmark folder deleted: {FolderId}", id);
    }
}
