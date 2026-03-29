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
public class CardServiceTests
{
    private TracksDbContext _db;
    private CardService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;
    private Board _board;
    private BoardList _list;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        _eventBusMock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var boardService = new BoardService(_db, _eventBusMock.Object, activityService, NullLogger<BoardService>.Instance);
        _service = new CardService(_db, boardService, activityService, _eventBusMock.Object, NullLogger<CardService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        _list = await TestHelpers.SeedListAsync(_db, _board.Id);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateCard_ValidDto_ReturnsCard()
    {
        var dto = new CreateCardDto
        {
            Title = "Task 1",
            Description = "Do the thing",
            Priority = CardPriority.High,
            AssigneeIds = [],
            LabelIds = []
        };

        var result = await _service.CreateCardAsync(_list.Id, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Task 1", result.Title);
        Assert.AreEqual("Do the thing", result.Description);
        Assert.AreEqual(CardPriority.High, result.Priority);
    }

    [TestMethod]
    public async Task CreateCard_WipLimitExceeded_Throws()
    {
        var limitList = await TestHelpers.SeedListAsync(_db, _board.Id, "Limited", cardLimit: 1);
        await TestHelpers.SeedCardAsync(_db, limitList.Id, _caller.UserId);

        var dto = new CreateCardDto { Title = "Over limit", AssigneeIds = [], LabelIds = [] };

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.CreateCardAsync(limitList.Id, dto, _caller));
    }

    [TestMethod]
    public async Task CreateCard_PublishesCardCreatedEvent()
    {
        var dto = new CreateCardDto { Title = "Event Card", AssigneeIds = [], LabelIds = [] };

        await _service.CreateCardAsync(_list.Id, dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<CardCreatedEvent>(e => e.Title == "Event Card"),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Get ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetCard_AsMember_ReturnsCard()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);

        var result = await _service.GetCardAsync(card.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(card.Title, result.Title);
    }

    [TestMethod]
    public async Task GetCard_NonMember_Throws()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);
        var otherCaller = TestHelpers.CreateCaller();

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.GetCardAsync(card.Id, otherCaller));
    }

    [TestMethod]
    public async Task GetCard_Deleted_ReturnsNull()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);
        card.IsDeleted = true;
        await _db.SaveChangesAsync();

        var result = await _service.GetCardAsync(card.Id, _caller);

        Assert.IsNull(result);
    }

    // ─── List Cards ──────────────────────────────────────────────────

    [TestMethod]
    public async Task ListCards_ReturnsCardsInOrder()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId, "Card 1");
        card1.Position = 1000;
        var card2 = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId, "Card 2");
        card2.Position = 2000;
        await _db.SaveChangesAsync();

        var results = await _service.ListCardsAsync(_list.Id, _caller);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("Card 1", results[0].Title);
        Assert.AreEqual("Card 2", results[1].Title);
    }

    // ─── Update ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateCard_ChangesFields()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);

        var result = await _service.UpdateCardAsync(card.Id,
            new UpdateCardDto { Title = "Updated", Priority = CardPriority.Urgent }, _caller);

        Assert.AreEqual("Updated", result.Title);
        Assert.AreEqual(CardPriority.Urgent, result.Priority);
    }

    // ─── Move ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task MoveCard_ToAnotherList_UpdatesList()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);
        var targetList = await TestHelpers.SeedListAsync(_db, _board.Id, "Done");

        var result = await _service.MoveCardAsync(card.Id,
            new MoveCardDto { TargetListId = targetList.Id, Position = 1000 }, _caller);

        Assert.AreEqual(targetList.Id, result.ListId);
    }

    [TestMethod]
    public async Task MoveCard_WipLimitOnTarget_Throws()
    {
        var targetList = await TestHelpers.SeedListAsync(_db, _board.Id, "Limited", cardLimit: 1);
        await TestHelpers.SeedCardAsync(_db, targetList.Id, _caller.UserId, "Existing");
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId, "Moving");

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.MoveCardAsync(card.Id,
                new MoveCardDto { TargetListId = targetList.Id, Position = 2000 }, _caller));
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteCard_SoftDeletes()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);

        await _service.DeleteCardAsync(card.Id, _caller);

        var dbCard = await _db.Cards.FindAsync(card.Id);
        Assert.IsTrue(dbCard!.IsDeleted);
    }

    [TestMethod]
    public async Task DeleteCard_PublishesEvent()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);

        await _service.DeleteCardAsync(card.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<CardDeletedEvent>(e => e.CardId == card.Id),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Assign / Unassign ───────────────────────────────────────────

    [TestMethod]
    public async Task AssignUser_AddsBoardMemberToCard()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);
        var assigneeId = Guid.NewGuid();
        await TestHelpers.AddMemberAsync(_db, _board.Id, assigneeId, BoardMemberRole.Member);

        await _service.AssignUserAsync(card.Id, assigneeId, _caller);

        var assignment = await _db.CardAssignments.FirstOrDefaultAsync(a => a.CardId == card.Id && a.UserId == assigneeId);
        Assert.IsNotNull(assignment);
    }

    [TestMethod]
    public async Task AssignUser_AlreadyAssigned_IsIdempotent()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);
        var assigneeId = Guid.NewGuid();
        await TestHelpers.AddMemberAsync(_db, _board.Id, assigneeId, BoardMemberRole.Member);

        await _service.AssignUserAsync(card.Id, assigneeId, _caller);
        await _service.AssignUserAsync(card.Id, assigneeId, _caller);

        var count = await _db.CardAssignments.CountAsync(a => a.CardId == card.Id && a.UserId == assigneeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task UnassignUser_RemovesAssignment()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);
        var assigneeId = Guid.NewGuid();
        await TestHelpers.AddMemberAsync(_db, _board.Id, assigneeId, BoardMemberRole.Member);
        await _service.AssignUserAsync(card.Id, assigneeId, _caller);

        await _service.UnassignUserAsync(card.Id, assigneeId, _caller);

        var exists = await _db.CardAssignments.AnyAsync(a => a.CardId == card.Id && a.UserId == assigneeId);
        Assert.IsFalse(exists);
    }

    [TestMethod]
    public async Task UnassignUser_NotAssigned_IsIdempotent()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId);

        // Should not throw
        await _service.UnassignUserAsync(card.Id, Guid.NewGuid(), _caller);
    }
}
