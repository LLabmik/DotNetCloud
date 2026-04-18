using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class BulkOperationServiceTests
{
    private TracksDbContext _db = null!;
    private BulkOperationService _service = null!;
    private CallerContext _caller;
    private Board _board = null!;
    private BoardSwimlane _swimlaneA = null!;
    private BoardSwimlane _swimlaneB = null!;
    private Card _card1 = null!;
    private Card _card2 = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        var mock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, mock.Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, mock.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new BulkOperationService(_db, boardService, activityService, NullLogger<BulkOperationService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        _swimlaneA = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id, "List A");
        _swimlaneB = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id, "List B");
        _card1 = await TestHelpers.SeedCardAsync(_db, _swimlaneA.Id, _caller.UserId, "Card 1");
        _card2 = await TestHelpers.SeedCardAsync(_db, _swimlaneA.Id, _caller.UserId, "Card 2");
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── BulkMove ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task BulkMove_SameBoard_MovesCards()
    {
        var dto = new BulkMoveCardsDto
        {
            CardIds = [_card1.Id, _card2.Id],
            TargetSwimlaneId = _swimlaneB.Id
        };

        var result = await _service.BulkMoveCardsAsync(dto, _caller);

        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(2, result.SuccessCount);
        var moved = await _db.Cards.FindAsync(_card1.Id);
        Assert.AreEqual(_swimlaneB.Id, moved!.SwimlaneId);
    }

    [TestMethod]
    public async Task BulkMove_EmptyCardList_Throws()
    {
        var dto = new BulkMoveCardsDto { CardIds = [], TargetSwimlaneId = _swimlaneB.Id };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.BulkMoveCardsAsync(dto, _caller));
    }

    [TestMethod]
    public async Task BulkMove_NonMember_Throws()
    {
        var outsider = TestHelpers.CreateCaller();
        var dto = new BulkMoveCardsDto { CardIds = [_card1.Id], TargetSwimlaneId = _swimlaneB.Id };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.BulkMoveCardsAsync(dto, outsider));
    }

    // ─── BulkAssign ───────────────────────────────────────────────────

    [TestMethod]
    public async Task BulkAssign_ValidCards_AssignsUser()
    {
        var userId = Guid.NewGuid();
        var dto = new BulkAssignCardsDto
        {
            CardIds = [_card1.Id, _card2.Id],
            UserId = userId
        };

        var result = await _service.BulkAssignCardsAsync(dto, _caller);

        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(2, result.SuccessCount);
    }

    [TestMethod]
    public async Task BulkAssign_EmptyCardList_Throws()
    {
        var dto = new BulkAssignCardsDto { CardIds = [], UserId = Guid.NewGuid() };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.BulkAssignCardsAsync(dto, _caller));
    }

    // ─── BulkLabel ────────────────────────────────────────────────────

    [TestMethod]
    public async Task BulkLabel_ValidLabel_AppliesLabel()
    {
        var label = new Label { BoardId = _board.Id, Title = "Bug", Color = "#ff0000" };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();

        var dto = new BulkLabelCardsDto
        {
            CardIds = [_card1.Id, _card2.Id],
            LabelId = label.Id
        };

        var result = await _service.BulkLabelCardsAsync(dto, _caller);

        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(2, result.SuccessCount);
    }

    [TestMethod]
    public async Task BulkLabel_EmptyCardList_Throws()
    {
        var dto = new BulkLabelCardsDto { CardIds = [], LabelId = Guid.NewGuid() };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.BulkLabelCardsAsync(dto, _caller));
    }

    // ─── BulkArchive ──────────────────────────────────────────────────

    [TestMethod]
    public async Task BulkArchive_ValidCards_ArchivesCards()
    {
        var dto = new BulkCardOperationDto { CardIds = [_card1.Id, _card2.Id] };

        var result = await _service.BulkArchiveCardsAsync(dto, _caller);

        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(2, result.SuccessCount);
        var archived1 = await _db.Cards.FindAsync(_card1.Id);
        Assert.IsTrue(archived1!.IsArchived);
    }

    [TestMethod]
    public async Task BulkArchive_EmptyCardList_Throws()
    {
        var dto = new BulkCardOperationDto { CardIds = [] };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.BulkArchiveCardsAsync(dto, _caller));
    }
}
