using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Bookmarks.Data;
using DotNetCloud.Modules.Bookmarks.Data.Services;
using DotNetCloud.Modules.Bookmarks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Bookmarks.Tests;

[TestClass]
public class BookmarkServiceTests
{
    private BookmarksDbContext _db;
    private BookmarkService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;
    private static readonly Guid UserId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<BookmarksDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new BookmarksDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _service = new BookmarkService(_db, _eventBusMock.Object, NullLogger<BookmarkService>.Instance);
        _caller = new CallerContext(UserId, new[] { "user" }, CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task CreateAsync_ValidRequest_ReturnsBookmarkWithGeneratedId()
    {
        var request = new CreateBookmarkRequest
        {
            Url = "https://example.com",
            Title = "Example Site",
            Description = "An example site for testing"
        };

        var result = await _service.CreateAsync(request, _caller);

        Assert.IsNotNull(result);
        Assert.AreNotEqual(Guid.Empty, result.Id);
        Assert.AreEqual("https://example.com", result.Url);
        Assert.AreEqual("Example Site", result.Title);
    }

    [TestMethod]
    public async Task CreateAsync_ValidRequest_PublishesSearchIndexRequestEvent()
    {
        var request = new CreateBookmarkRequest
        {
            Url = "https://example.com",
            Title = "Example"
        };

        await _service.CreateAsync(request, _caller);

        _eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<SearchIndexRequestEvent>(),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ListAsync_NoBookmarks_ReturnsEmptyList()
    {
        var results = await _service.ListAsync(_caller, null, 0, 50);

        Assert.IsNotNull(results);
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task ListAsync_MultipleBookmarks_ReturnsAllOwnedByCaller()
    {
        await _service.CreateAsync(new CreateBookmarkRequest { Url = "https://a.com", Title = "A" }, _caller);
        await _service.CreateAsync(new CreateBookmarkRequest { Url = "https://b.com", Title = "B" }, _caller);
        await _service.CreateAsync(new CreateBookmarkRequest { Url = "https://c.com", Title = "C" }, _caller);

        var results = await _service.ListAsync(_caller, null, 0, 50);

        Assert.AreEqual(3, results.Count);
    }

    [TestMethod]
    public async Task ListAsync_OtherUsersBookmarks_NotReturned()
    {
        await _service.CreateAsync(new CreateBookmarkRequest { Url = "https://a.com", Title = "A" }, _caller);
        var otherCaller = new CallerContext(Guid.NewGuid(), new[] { "user" }, CallerType.User);
        await _service.CreateAsync(new CreateBookmarkRequest { Url = "https://b.com", Title = "B" }, otherCaller);

        var results = await _service.ListAsync(_caller, null, 0, 50);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("https://a.com", results[0].Url);
    }

    [TestMethod]
    public async Task GetAsync_ExistingBookmark_ReturnsBookmark()
    {
        var created = await _service.CreateAsync(
            new CreateBookmarkRequest { Url = "https://example.com", Title = "Test" }, _caller);

        var result = await _service.GetAsync(created.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(created.Id, result.Id);
    }

    [TestMethod]
    public async Task GetAsync_NonExistentBookmark_ReturnsNull()
    {
        var result = await _service.GetAsync(Guid.NewGuid(), _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateAsync_ExistingBookmark_UpdatesFields()
    {
        var created = await _service.CreateAsync(
            new CreateBookmarkRequest { Url = "https://example.com", Title = "Original" }, _caller);

        var updated = await _service.UpdateAsync(created.Id, new UpdateBookmarkRequest
        {
            Title = "Updated Title",
            Description = "Updated Description"
        }, _caller);

        Assert.AreEqual("Updated Title", updated.Title);
        Assert.AreEqual("https://example.com", updated.Url);
    }

    [TestMethod]
    public async Task DeleteAsync_ExistingBookmark_SetsSoftDelete()
    {
        var created = await _service.CreateAsync(
            new CreateBookmarkRequest { Url = "https://example.com", Title = "Test" }, _caller);

        await _service.DeleteAsync(created.Id, _caller);

        var result = await _service.GetAsync(created.Id, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SearchAsync_MatchingQuery_ReturnsFilteredResults()
    {
        await _service.CreateAsync(new CreateBookmarkRequest { Url = "https://cats.com", Title = "Cat Pictures" }, _caller);
        await _service.CreateAsync(new CreateBookmarkRequest { Url = "https://dogs.com", Title = "Dog Pictures" }, _caller);

        var results = await _service.SearchAsync(_caller, "cat", 0, 50);

        Assert.AreEqual(1, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_EmptyQuery_ReturnsAll()
    {
        await _service.CreateAsync(new CreateBookmarkRequest { Url = "https://a.com", Title = "A" }, _caller);
        await _service.CreateAsync(new CreateBookmarkRequest { Url = "https://b.com", Title = "B" }, _caller);

        var results = await _service.SearchAsync(_caller, "", 0, 50);

        Assert.AreEqual(2, results.Count);
    }
}
