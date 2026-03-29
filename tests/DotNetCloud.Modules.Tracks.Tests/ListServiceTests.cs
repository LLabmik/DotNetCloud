using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class ListServiceTests
{
    private TracksDbContext _db;
    private ListService _service;
    private BoardService _boardService;
    private CallerContext _caller;
    private Board _board;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, new Mock<IEventBus>().Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, new Mock<IEventBus>().Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new ListService(_db, _boardService, activityService, NullLogger<ListService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateList_ValidDto_ReturnsList()
    {
        var dto = new CreateBoardListDto { Title = "To Do", Color = "#00FF00", CardLimit = 5 };

        var result = await _service.CreateListAsync(_board.Id, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("To Do", result.Title);
        Assert.AreEqual("#00FF00", result.Color);
        Assert.AreEqual(5, result.CardLimit);
        Assert.AreEqual(_board.Id, result.BoardId);
    }

    [TestMethod]
    public async Task CreateList_PositionAppendsAfterLast()
    {
        await _service.CreateListAsync(_board.Id, new CreateBoardListDto { Title = "First" }, _caller);
        var second = await _service.CreateListAsync(_board.Id, new CreateBoardListDto { Title = "Second" }, _caller);

        Assert.IsTrue(second.Position > 0);
    }

    [TestMethod]
    public async Task CreateList_AsViewer_Throws()
    {
        var viewerCaller = TestHelpers.CreateCaller();
        await TestHelpers.AddMemberAsync(_db, _board.Id, viewerCaller.UserId, BoardMemberRole.Viewer);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.CreateListAsync(_board.Id, new CreateBoardListDto { Title = "Nope" }, viewerCaller));
    }

    // ─── Get Lists ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetLists_ReturnsOrderedLists()
    {
        await _service.CreateListAsync(_board.Id, new CreateBoardListDto { Title = "A" }, _caller);
        await _service.CreateListAsync(_board.Id, new CreateBoardListDto { Title = "B" }, _caller);

        var results = await _service.GetListsAsync(_board.Id, _caller);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("A", results[0].Title);
        Assert.AreEqual("B", results[1].Title);
    }

    [TestMethod]
    public async Task GetLists_ExcludesArchived()
    {
        var list = await TestHelpers.SeedListAsync(_db, _board.Id, "Archived");
        list.IsArchived = true;
        await _db.SaveChangesAsync();

        var results = await _service.GetListsAsync(_board.Id, _caller);

        Assert.AreEqual(0, results.Count);
    }

    // ─── Update ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateList_ChangesTitle()
    {
        var list = await TestHelpers.SeedListAsync(_db, _board.Id);

        var result = await _service.UpdateListAsync(list.Id, new UpdateBoardListDto { Title = "Updated" }, _caller);

        Assert.AreEqual("Updated", result.Title);
    }

    // ─── Delete (Archive) ────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteList_ArchivesList()
    {
        var list = await TestHelpers.SeedListAsync(_db, _board.Id);

        await _service.DeleteListAsync(list.Id, _caller);

        var dbList = await _db.BoardLists.FindAsync(list.Id);
        Assert.IsTrue(dbList!.IsArchived);
    }

    [TestMethod]
    public async Task DeleteList_AsMember_Throws()
    {
        var memberCaller = TestHelpers.CreateCaller();
        await TestHelpers.AddMemberAsync(_db, _board.Id, memberCaller.UserId, BoardMemberRole.Member);
        var list = await TestHelpers.SeedListAsync(_db, _board.Id);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.DeleteListAsync(list.Id, memberCaller));
    }

    // ─── Reorder ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task ReorderLists_UpdatesPositions()
    {
        var list1 = await TestHelpers.SeedListAsync(_db, _board.Id, "First");
        var list2 = await TestHelpers.SeedListAsync(_db, _board.Id, "Second");

        await _service.ReorderListsAsync(_board.Id, [list2.Id, list1.Id], _caller);

        var reordered1 = await _db.BoardLists.FindAsync(list1.Id);
        var reordered2 = await _db.BoardLists.FindAsync(list2.Id);
        Assert.IsTrue(reordered2!.Position < reordered1!.Position);
    }
}
