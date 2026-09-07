using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Bookmarks.Data;
using DotNetCloud.Modules.Bookmarks.Data.Services;
using DotNetCloud.Modules.Bookmarks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Bookmarks.Tests;

/// <summary>
/// Tests for <see cref="BookmarkService.GetRecentBookmarksAsync"/>.
/// </summary>
[TestClass]
public class BookmarkServiceGetRecentBookmarksTests
{
    private BookmarksDbContext _db = null!;
    private BookmarkService _service = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<BookmarksDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new BookmarksDbContext(options);
        _service = new BookmarkService(
            _db,
            new Mock<IEventBus>().Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            NullLogger<BookmarkService>.Instance);
        _caller = new CallerContext(Guid.CreateVersion7(), new[] { "user" }, CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task SeedBookmarkAsync(Guid ownerId, string title, DateTime createdAt)
    {
        _db.Bookmarks.Add(new BookmarkItem
        {
            OwnerId = ownerId,
            Title = title,
            Url = $"https://{title.ToLowerInvariant()}.example.com",
            CreatedAt = createdAt
        });
        await _db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task GetRecentBookmarks_NewestFirst_ReturnsBookmarksOrderedByCreatedAtDescending()
    {
        var now = DateTime.UtcNow;

        await SeedBookmarkAsync(_caller.UserId, "Oldest", now.AddDays(-3));
        await SeedBookmarkAsync(_caller.UserId, "Middle", now.AddDays(-2));
        await SeedBookmarkAsync(_caller.UserId, "Newest", now.AddDays(-1));

        var result = await _service.GetRecentBookmarksAsync(_caller);

        CollectionAssert.AreEqual(
            new[] { "Newest", "Middle", "Oldest" },
            result.Select(b => b.Title).ToArray());
    }

    [TestMethod]
    public async Task GetRecentBookmarks_RespectsCount_ReturnsOnlyRequestedNumberOfNewest()
    {
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
            await SeedBookmarkAsync(_caller.UserId, $"Bookmark{i}", now.AddMinutes(i));

        var result = await _service.GetRecentBookmarksAsync(_caller, count: 2);

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual(
            new[] { "Bookmark4", "Bookmark3" },
            result.Select(b => b.Title).ToArray());
    }

    [TestMethod]
    public async Task GetRecentBookmarks_OwnerScoped_ExcludesOtherUsersBookmarks()
    {
        var now = DateTime.UtcNow;

        await SeedBookmarkAsync(_caller.UserId, "Mine", now);
        await SeedBookmarkAsync(Guid.CreateVersion7(), "Theirs", now);

        var result = await _service.GetRecentBookmarksAsync(_caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Mine", result[0].Title);
    }

    [TestMethod]
    public async Task GetRecentBookmarks_NoBookmarks_ReturnsEmptyList()
    {
        var result = await _service.GetRecentBookmarksAsync(_caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }
}
