using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class ChecklistServiceTests
{
    private TracksDbContext _db;
    private ChecklistService _service;
    private CallerContext _caller;
    private Board _board;
    private Card _card;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, new Mock<IEventBus>().Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, new Mock<IEventBus>().Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new ChecklistService(_db, boardService, activityService, NullLogger<ChecklistService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        var list = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        _card = await TestHelpers.SeedCardAsync(_db, list.Id, _caller.UserId);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create Checklist ─────────────────────────────────────────────

    [TestMethod]
    public async Task CreateChecklist_ValidTitle_ReturnsChecklist()
    {
        var result = await _service.CreateChecklistAsync(_card.Id, "Requirements", _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Requirements", result.Title);
        Assert.AreEqual(_card.Id, result.CardId);
    }

    [TestMethod]
    public async Task CreateChecklist_PositionAppendsAfterLast()
    {
        var first = await _service.CreateChecklistAsync(_card.Id, "First", _caller);
        var second = await _service.CreateChecklistAsync(_card.Id, "Second", _caller);

        Assert.IsTrue(second.Position > first.Position);
    }

    // ─── Get Checklists ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetChecklists_ReturnsOrderedWithItems()
    {
        var cl = await _service.CreateChecklistAsync(_card.Id, "CL", _caller);
        await _service.AddItemAsync(cl.Id, "Item 1", _caller);
        await _service.AddItemAsync(cl.Id, "Item 2", _caller);

        var results = await _service.GetChecklistsAsync(_card.Id, _caller);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(2, results[0].Items.Count);
    }

    // ─── Delete Checklist ─────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteChecklist_RemovesChecklistAndItems()
    {
        var cl = await _service.CreateChecklistAsync(_card.Id, "Doomed", _caller);
        await _service.AddItemAsync(cl.Id, "Item", _caller);

        await _service.DeleteChecklistAsync(cl.Id, _caller);

        Assert.IsFalse(await _db.CardChecklists.AnyAsync(c => c.Id == cl.Id));
        Assert.IsFalse(await _db.ChecklistItems.AnyAsync(i => i.ChecklistId == cl.Id));
    }

    // ─── Add Item ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task AddItem_ValidTitle_ReturnsItem()
    {
        var cl = await _service.CreateChecklistAsync(_card.Id, "CL", _caller);

        var result = await _service.AddItemAsync(cl.Id, "Do something", _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Do something", result.Title);
        Assert.IsFalse(result.IsCompleted);
    }

    // ─── Toggle Item ──────────────────────────────────────────────────

    [TestMethod]
    public async Task ToggleItem_FlipsCompletionStatus()
    {
        var cl = await _service.CreateChecklistAsync(_card.Id, "CL", _caller);
        var item = await _service.AddItemAsync(cl.Id, "Toggle me", _caller);

        var toggled = await _service.ToggleItemAsync(item.Id, _caller);
        Assert.IsTrue(toggled.IsCompleted);

        var toggledBack = await _service.ToggleItemAsync(item.Id, _caller);
        Assert.IsFalse(toggledBack.IsCompleted);
    }

    // ─── Delete Item ──────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteItem_RemovesItem()
    {
        var cl = await _service.CreateChecklistAsync(_card.Id, "CL", _caller);
        var item = await _service.AddItemAsync(cl.Id, "Doomed", _caller);

        await _service.DeleteItemAsync(item.Id, _caller);

        Assert.IsFalse(await _db.ChecklistItems.AnyAsync(i => i.Id == item.Id));
    }
}
