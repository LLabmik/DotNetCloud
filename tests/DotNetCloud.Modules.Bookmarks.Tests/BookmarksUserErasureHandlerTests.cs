using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Bookmarks.Data;
using DotNetCloud.Modules.Bookmarks.Host.Services;
using DotNetCloud.Modules.Bookmarks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Bookmarks.Tests;

/// <summary>
/// Tests for <see cref="BookmarksUserErasureHandler"/> (F12). The module owns an isolated data store,
/// so it must delete exactly one account's rows — including rows the user had soft-deleted — and
/// report how many it removed.
/// </summary>
[TestClass]
public class BookmarksUserErasureHandlerTests
{
    private ServiceProvider _provider = null!;
    private string _scopeKey = null!;

    [TestInitialize]
    public void Setup()
    {
        // A fixed database name per test: the options lambda runs on every scope resolution.
        _scopeKey = Guid.CreateVersion7().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<BookmarksDbContext>(options => options.UseInMemoryDatabase(_scopeKey));
        _provider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup() => _provider.Dispose();

    [TestMethod]
    public async Task EraseAsync_UserWithBookmarksAndFolders_DeletesOnlyThatUsersData()
    {
        var erased = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await SeedAsync(erased, bookmarks: 3, folders: 2, previewsPerBookmark: 1);
        await SeedAsync(other, bookmarks: 2, folders: 1, previewsPerBookmark: 1);

        var result = await CreateHandler().EraseAsync(Context(erased));

        Assert.IsTrue(result.Acknowledged);
        Assert.AreEqual(8, result.DeletedCounts[BookmarksUserErasureHandler.AllScopeKey], "3 bookmarks + 3 previews + 2 folders");
        Assert.AreEqual(0, await CountBookmarksAsync(erased));
        Assert.AreEqual(0, await CountFoldersAsync(erased));
        Assert.AreEqual(2, await CountBookmarksAsync(other), "another user's bookmarks must survive");
        Assert.AreEqual(1, await CountFoldersAsync(other));
    }

    [TestMethod]
    public async Task EraseAsync_SoftDeletedRows_AreAlsoErased()
    {
        var erased = Guid.CreateVersion7();

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookmarksDbContext>();
            db.Bookmarks.Add(new BookmarkItem
            {
                OwnerId = erased,
                Url = "https://example.com/hidden",
                NormalizedUrl = "https://example.com/hidden",
                Title = "Soft deleted",
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await CreateHandler().EraseAsync(Context(erased));

        // Counted with the query filter disabled: a "deleted" row is still personal data.
        using var verify = _provider.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<BookmarksDbContext>();
        Assert.AreEqual(0, await verifyDb.Bookmarks.IgnoreQueryFilters().CountAsync(b => b.OwnerId == erased));
    }

    [TestMethod]
    public async Task EraseAsync_SameUserTwice_IsIdempotent()
    {
        var erased = Guid.CreateVersion7();
        await SeedAsync(erased, bookmarks: 2, folders: 0, previewsPerBookmark: 0);

        var handler = CreateHandler();
        var first = await handler.EraseAsync(Context(erased));
        var second = await handler.EraseAsync(Context(erased));

        Assert.AreEqual(2, first.DeletedCounts[BookmarksUserErasureHandler.AllScopeKey]);
        Assert.AreEqual(0, second.DeletedCounts[BookmarksUserErasureHandler.AllScopeKey]);
        Assert.IsTrue(second.Acknowledged);
    }

    [TestMethod]
    public async Task EraseAsync_UserWithNothing_AnswersWithZeroCount()
    {
        var result = await CreateHandler().EraseAsync(Context(Guid.CreateVersion7()));

        Assert.IsTrue(result.Acknowledged);
        Assert.IsFalse(result.NotApplicable);
        Assert.AreEqual(0, result.DeletedCounts[BookmarksUserErasureHandler.AllScopeKey]);
    }

    private BookmarksUserErasureHandler CreateHandler() =>
        new(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<BookmarksUserErasureHandler>.Instance);

    private static ModuleUserErasureContext Context(Guid userId) => new(Guid.CreateVersion7(), userId, []);

    private async Task SeedAsync(Guid ownerId, int bookmarks, int folders, int previewsPerBookmark)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookmarksDbContext>();

        for (var index = 0; index < folders; index++)
        {
            db.BookmarkFolders.Add(new BookmarkFolder { OwnerId = ownerId, Name = $"Folder {index}" });
        }

        for (var index = 0; index < bookmarks; index++)
        {
            var bookmark = new BookmarkItem
            {
                OwnerId = ownerId,
                Url = $"https://example.com/{index}",
                NormalizedUrl = $"https://example.com/{index}",
                Title = $"Bookmark {index}",
            };
            db.Bookmarks.Add(bookmark);

            for (var preview = 0; preview < previewsPerBookmark; preview++)
            {
                db.BookmarkPreviews.Add(new BookmarkPreview { BookmarkId = bookmark.Id });
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> CountBookmarksAsync(Guid ownerId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookmarksDbContext>();
        return await db.Bookmarks.IgnoreQueryFilters().CountAsync(b => b.OwnerId == ownerId);
    }

    private async Task<int> CountFoldersAsync(Guid ownerId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookmarksDbContext>();
        return await db.BookmarkFolders.IgnoreQueryFilters().CountAsync(f => f.OwnerId == ownerId);
    }
}
