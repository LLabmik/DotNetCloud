using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Bookmarks.Data;
using DotNetCloud.Modules.Bookmarks.Data.Services;
using DotNetCloud.Modules.Bookmarks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Bookmarks.Tests;

/// <summary>
/// Tests for <see cref="BookmarkFolderService"/> folder hierarchy operations.
/// </summary>
[TestClass]
public class BookmarkFolderServiceTests
{
    private BookmarksDbContext _db;
    private BookmarkFolderService _service;
    private CallerContext _caller;
    private static readonly Guid UserId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<BookmarksDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new BookmarksDbContext(options);
        _service = new BookmarkFolderService(_db, NullLogger<BookmarkFolderService>.Instance);
        _caller = new CallerContext(UserId, ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task CreateAsync_ValidRequest_ReturnsFolder()
    {
        var result = await _service.CreateAsync(
            new CreateBookmarkFolderRequest { Name = "My Folder" }, _caller);

        Assert.IsNotNull(result);
        Assert.AreNotEqual(Guid.Empty, result.Id);
        Assert.AreEqual("My Folder", result.Name);
        Assert.AreEqual(UserId, result.OwnerId);
    }

    [TestMethod]
    public async Task CreateAsync_NestedFolder_SetsParentId()
    {
        var parent = await _service.CreateAsync(
            new CreateBookmarkFolderRequest { Name = "Parent" }, _caller);

        var child = await _service.CreateAsync(
            new CreateBookmarkFolderRequest { Name = "Child", ParentId = parent.Id }, _caller);

        Assert.AreEqual(parent.Id, child.ParentId);
    }

    [TestMethod]
    public async Task ListAsync_RootFolders_ReturnsOnlyTopLevel()
    {
        var root = await _service.CreateAsync(new CreateBookmarkFolderRequest { Name = "Root" }, _caller);
        await _service.CreateAsync(new CreateBookmarkFolderRequest { Name = "Child", ParentId = root.Id }, _caller);

        var roots = await _service.ListAsync(_caller, null);

        Assert.AreEqual(1, roots.Count);
        Assert.AreEqual("Root", roots[0].Name);
    }

    [TestMethod]
    public async Task ListAsync_ChildFolders_ReturnsNested()
    {
        var parent = await _service.CreateAsync(new CreateBookmarkFolderRequest { Name = "Parent" }, _caller);
        var child1 = await _service.CreateAsync(new CreateBookmarkFolderRequest { Name = "Child 1", ParentId = parent.Id }, _caller);
        await _service.CreateAsync(new CreateBookmarkFolderRequest { Name = "Child 2", ParentId = parent.Id }, _caller);

        var children = await _service.ListAsync(_caller, parent.Id);

        Assert.AreEqual(2, children.Count);
    }

    [TestMethod]
    public async Task GetAsync_ExistingFolder_ReturnsFolder()
    {
        var created = await _service.CreateAsync(
            new CreateBookmarkFolderRequest { Name = "Test Folder" }, _caller);

        var result = await _service.GetAsync(created.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(created.Id, result.Id);
    }

    [TestMethod]
    public async Task GetAsync_NonExistentFolder_ReturnsNull()
    {
        var result = await _service.GetAsync(Guid.NewGuid(), _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAsync_ExistingFolder_SoftDeletes()
    {
        var created = await _service.CreateAsync(
            new CreateBookmarkFolderRequest { Name = "To Delete" }, _caller);

        await _service.DeleteAsync(created.Id, _caller);

        var result = await _service.GetAsync(created.Id, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateAsync_RenameFolder_UpdatesName()
    {
        var created = await _service.CreateAsync(
            new CreateBookmarkFolderRequest { Name = "Old Name" }, _caller);

        var updated = await _service.UpdateAsync(created.Id,
            new UpdateBookmarkFolderRequest { Name = "New Name" }, _caller);

        Assert.AreEqual("New Name", updated.Name);
    }

    [TestMethod]
    public async Task ListAsync_OtherUsersFolders_NotReturned()
    {
        await _service.CreateAsync(new CreateBookmarkFolderRequest { Name = "Mine" }, _caller);
        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        await _service.CreateAsync(new CreateBookmarkFolderRequest { Name = "Yours" }, otherCaller);

        var mine = await _service.ListAsync(_caller, null);

        Assert.AreEqual(1, mine.Count);
        Assert.AreEqual("Mine", mine[0].Name);
    }
}
