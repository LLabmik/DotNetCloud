using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Bookmarks.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks.Host.Services;

/// <summary>
/// Erases a deleted account's bookmarks, folders and previews on behalf of the core's
/// account-erasure orchestrator.
/// </summary>
/// <remarks>
/// <para>
/// The module declares <c>dataStore: "isolated"</c>, so every bookmark row lives in a schema only
/// this module can reach. Deleting the account must therefore delete them here (F12); the core
/// records the acknowledgement and retries for 72 hours, and an account deletion never reports
/// success while a module row is outstanding.
/// </para>
/// <para>
/// <c>IgnoreQueryFilters</c> is deliberate: <c>BookmarkItem</c> and <c>BookmarkFolder</c> carry a
/// soft-delete filter, and a row a user had already "deleted" in the UI is still personal data that
/// must not survive an account deletion.
/// </para>
/// </remarks>
public sealed class BookmarksUserErasureHandler : IModuleUserErasureHandler
{
    /// <summary>Scope key used when reporting how many rows were deleted.</summary>
    public const string AllScopeKey = "all";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookmarksUserErasureHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="BookmarksUserErasureHandler"/> class.</summary>
    /// <param name="scopeFactory">Creates the scope the scoped <see cref="BookmarksDbContext"/> comes from.</param>
    /// <param name="logger">The logger.</param>
    public BookmarksUserErasureHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<BookmarksUserErasureHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ModuleUserErasureResult> EraseAsync(
        ModuleUserErasureContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookmarksDbContext>();

        var bookmarks = await db.Bookmarks
            .IgnoreQueryFilters()
            .Where(bookmark => bookmark.OwnerId == context.UserId)
            .ToListAsync(cancellationToken);

        var deleted = bookmarks.Count;

        if (bookmarks.Count > 0)
        {
            var bookmarkIds = bookmarks.Select(bookmark => bookmark.Id).ToList();

            var previews = await db.BookmarkPreviews
                .Where(preview => bookmarkIds.Contains(preview.BookmarkId))
                .ToListAsync(cancellationToken);

            db.BookmarkPreviews.RemoveRange(previews);
            db.Bookmarks.RemoveRange(bookmarks);
            deleted += previews.Count;
        }

        var folders = await db.BookmarkFolders
            .IgnoreQueryFilters()
            .Where(folder => folder.OwnerId == context.UserId)
            .ToListAsync(cancellationToken);

        if (folders.Count > 0)
        {
            db.BookmarkFolders.RemoveRange(folders);
            deleted += folders.Count;
        }

        if (deleted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        // The account id is personal data: it is deliberately never logged
        // (cs/exposure-of-sensitive-information).
        _logger.LogInformation(
            "Erased {RowCount} bookmark row(s) for a deleted account.",
            deleted);

        return ModuleUserErasureResult.Success(
            new Dictionary<string, int>(StringComparer.Ordinal) { [AllScopeKey] = deleted });
    }
}
