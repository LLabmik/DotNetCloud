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
public class LabelServiceTests
{
    private TracksDbContext _db;
    private LabelService _service;
    private CallerContext _caller;
    private Board _board;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, new Mock<IEventBus>().Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, new Mock<IEventBus>().Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new LabelService(_db, boardService, activityService, NullLogger<LabelService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateLabel_ValidDto_ReturnsLabel()
    {
        var dto = new CreateLabelDto { Title = "Bug", Color = "#EF4444" };

        var result = await _service.CreateLabelAsync(_board.Id, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Bug", result.Title);
        Assert.AreEqual("#EF4444", result.Color);
        Assert.AreEqual(_board.Id, result.BoardId);
    }

    [TestMethod]
    public async Task CreateLabel_AsMember_Throws()
    {
        var memberCaller = TestHelpers.CreateCaller();
        await TestHelpers.AddMemberAsync(_db, _board.Id, memberCaller.UserId, BoardMemberRole.Member);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.CreateLabelAsync(_board.Id, new CreateLabelDto { Title = "X", Color = "#000" }, memberCaller));
    }

    // ─── Get Labels ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetLabels_ReturnsLabelsAlphabetical()
    {
        _db.Labels.Add(new Label { BoardId = _board.Id, Title = "Zeta", Color = "#000" });
        _db.Labels.Add(new Label { BoardId = _board.Id, Title = "Alpha", Color = "#FFF" });
        await _db.SaveChangesAsync();

        var results = await _service.GetLabelsAsync(_board.Id, _caller);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("Alpha", results[0].Title);
        Assert.AreEqual("Zeta", results[1].Title);
    }

    // ─── Update ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateLabel_ChangesFields()
    {
        var label = new Label { BoardId = _board.Id, Title = "Old", Color = "#000" };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();

        var result = await _service.UpdateLabelAsync(label.Id, new UpdateLabelDto { Title = "New", Color = "#FFF" }, _caller);

        Assert.AreEqual("New", result.Title);
        Assert.AreEqual("#FFF", result.Color);
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteLabel_RemovesLabelAndCardAssociations()
    {
        var label = new Label { BoardId = _board.Id, Title = "Doomed", Color = "#000" };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();

        var list = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, list.Id, _caller.UserId);
        _db.CardLabels.Add(new CardLabel { CardId = card.Id, LabelId = label.Id });
        await _db.SaveChangesAsync();

        await _service.DeleteLabelAsync(label.Id, _caller);

        Assert.IsFalse(await _db.Labels.AnyAsync(l => l.Id == label.Id));
        Assert.IsFalse(await _db.CardLabels.AnyAsync(cl => cl.LabelId == label.Id));
    }

    // ─── Add/Remove Label from Card ──────────────────────────────────

    [TestMethod]
    public async Task AddLabelToCard_AppliesLabel()
    {
        var label = new Label { BoardId = _board.Id, Title = "Tag", Color = "#000" };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();

        var list = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, list.Id, _caller.UserId);

        await _service.AddLabelToCardAsync(card.Id, label.Id, _caller);

        Assert.IsTrue(await _db.CardLabels.AnyAsync(cl => cl.CardId == card.Id && cl.LabelId == label.Id));
    }

    [TestMethod]
    public async Task AddLabelToCard_AlreadyApplied_IsIdempotent()
    {
        var label = new Label { BoardId = _board.Id, Title = "Tag", Color = "#000" };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();

        var list = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, list.Id, _caller.UserId);

        await _service.AddLabelToCardAsync(card.Id, label.Id, _caller);
        await _service.AddLabelToCardAsync(card.Id, label.Id, _caller);

        var count = await _db.CardLabels.CountAsync(cl => cl.CardId == card.Id && cl.LabelId == label.Id);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task RemoveLabelFromCard_RemovesAssociation()
    {
        var label = new Label { BoardId = _board.Id, Title = "Tag", Color = "#000" };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();

        var list = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, list.Id, _caller.UserId);
        _db.CardLabels.Add(new CardLabel { CardId = card.Id, LabelId = label.Id });
        await _db.SaveChangesAsync();

        await _service.RemoveLabelFromCardAsync(card.Id, label.Id, _caller);

        Assert.IsFalse(await _db.CardLabels.AnyAsync(cl => cl.CardId == card.Id && cl.LabelId == label.Id));
    }

    [TestMethod]
    public async Task RemoveLabelFromCard_NotApplied_IsIdempotent()
    {
        var list = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, list.Id, _caller.UserId);

        // Should not throw
        await _service.RemoveLabelFromCardAsync(card.Id, Guid.NewGuid(), _caller);
    }
}
