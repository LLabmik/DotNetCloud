using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Notes.Tests;

[TestClass]
public class NoteFolderServiceTests
{
    private NotesDbContext _db;
    private NoteFolderService _service;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotesDbContext(options);
        _service = new NoteFolderService(_db, NullLogger<NoteFolderService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateFolder_ReturnsFolder()
    {
        var dto = new CreateNoteFolderDto { Name = "Work" };

        var result = await _service.CreateFolderAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Work", result.Name);
        Assert.AreEqual(_caller.UserId, result.OwnerId);
        Assert.IsNull(result.ParentId);
    }

    [TestMethod]
    public async Task CreateFolder_WithParent_SetsParentId()
    {
        var parent = await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Parent" }, _caller);

        var child = await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Child", ParentId = parent.Id }, _caller);

        Assert.AreEqual(parent.Id, child.ParentId);
    }

    [TestMethod]
    public async Task CreateFolder_DuplicateName_Throws()
    {
        await _service.CreateFolderAsync(new CreateNoteFolderDto { Name = "Dupe" }, _caller);

        var ex = await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.CreateFolderAsync(new CreateNoteFolderDto { Name = "Dupe" }, _caller));

        Assert.AreEqual(Core.Errors.ErrorCodes.ValidationError, ex.ErrorCode);
    }

    [TestMethod]
    public async Task CreateFolder_SameNameDifferentParent_Succeeds()
    {
        var parent1 = await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Parent1" }, _caller);
        var parent2 = await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Parent2" }, _caller);

        var child1 = await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Same", ParentId = parent1.Id }, _caller);
        var child2 = await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Same", ParentId = parent2.Id }, _caller);

        Assert.AreNotEqual(child1.Id, child2.Id);
    }

    // ─── List ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListFolders_ReturnsOwnFolders()
    {
        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await _service.CreateFolderAsync(new CreateNoteFolderDto { Name = "Mine" }, _caller);
        await _service.CreateFolderAsync(new CreateNoteFolderDto { Name = "Theirs" }, otherCaller);

        var results = await _service.ListFoldersAsync(_caller);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Mine", results[0].Name);
    }

    [TestMethod]
    public async Task ListFolders_FilterByParent()
    {
        var parent = await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Parent" }, _caller);
        await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Child", ParentId = parent.Id }, _caller);
        await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Sibling" }, _caller);

        var results = await _service.ListFoldersAsync(_caller, parentId: parent.Id);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Child", results[0].Name);
    }

    // ─── Update ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateFolder_ChangesName()
    {
        var created = await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Old" }, _caller);

        var result = await _service.UpdateFolderAsync(created.Id,
            new UpdateNoteFolderDto { Name = "New" }, _caller);

        Assert.AreEqual("New", result.Name);
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteFolder_SoftDeletes()
    {
        var created = await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Doomed" }, _caller);

        await _service.DeleteFolderAsync(created.Id, _caller);

        var result = await _service.GetFolderAsync(created.Id, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteFolder_OtherUser_Throws()
    {
        var created = await _service.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "Protected" }, _caller);

        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.DeleteFolderAsync(created.Id, otherCaller));
    }
}
