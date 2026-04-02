using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Controllers;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Comprehensive tests for Phase G: Backlog &amp; Sprint Views.
/// Validates backlog card retrieval, sprint-filtered card listing,
/// bulk sprint assignment, backlog/sprint card lifecycle,
/// controller endpoints, and mode guards.
/// </summary>
[TestClass]
public class PhaseG_BacklogSprintViewTests
{
    private TracksDbContext _db = null!;
    private BoardService _boardService = null!;
    private CardService _cardService = null!;
    private SwimlaneService _swimlaneService = null!;
    private SprintService _sprintService = null!;
    private LabelService _labelService = null!;
    private SprintPlanningService _sprintPlanningService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private NullTracksRealtimeService _realtimeService = null!;
    private ActivityService _activityService = null!;
    private SprintsController _sprintsController = null!;
    private BoardBacklogController _backlogController = null!;
    private CardsController _cardsController = null!;
    private CallerContext _admin = null!;
    private CallerContext _member = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();

    private Board _teamBoard = null!;
    private Board _personalBoard = null!;
    private BoardSwimlane _todoSwimlane = null!;
    private BoardSwimlane _doneSwimlane = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _realtimeService = new NullTracksRealtimeService();
        _activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, _eventBusMock.Object, _activityService, teamService, NullLogger<BoardService>.Instance);
        _swimlaneService = new SwimlaneService(_db, _boardService, _activityService, NullLogger<SwimlaneService>.Instance);
        _cardService = new CardService(_db, _boardService, _activityService, _eventBusMock.Object, NullLogger<CardService>.Instance);
        _labelService = new LabelService(_db, _boardService, _activityService, NullLogger<LabelService>.Instance);
        _sprintService = new SprintService(_db, _boardService, _activityService, _eventBusMock.Object, NullLogger<SprintService>.Instance);
        _sprintPlanningService = new SprintPlanningService(_db, _boardService, _activityService, NullLogger<SprintPlanningService>.Instance);

        _sprintsController = new SprintsController(_sprintService, _sprintPlanningService, NullLogger<SprintsController>.Instance);
        _backlogController = new BoardBacklogController(_sprintService);
        _cardsController = new CardsController(_cardService, _labelService, _activityService, NullLogger<CardsController>.Instance);
        SetupControllerContext(_sprintsController, _adminUserId);
        SetupControllerContext(_backlogController, _adminUserId);
        SetupControllerContext(_cardsController, _adminUserId);

        _admin = TestHelpers.CreateCaller(_adminUserId);
        _member = TestHelpers.CreateCaller(_memberUserId);

        // Seed Team board with admin + member
        _teamBoard = await TestHelpers.SeedBoardAsync(_db, _adminUserId, "Team Board");
        _teamBoard.Mode = BoardMode.Team;
        _db.Update(_teamBoard);
        await TestHelpers.AddMemberAsync(_db, _teamBoard.Id, _memberUserId, BoardMemberRole.Member);
        await _db.SaveChangesAsync();

        // Seed swimlanes
        _todoSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, _teamBoard.Id, "To Do");
        _doneSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, _teamBoard.Id, "Done");

        // Seed Personal board
        _personalBoard = await TestHelpers.SeedBoardAsync(_db, _adminUserId, "Personal Board");
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ═══════════════════════════════════════════════════════════════════
    // Backlog Card Retrieval (Step 27 — BacklogView backend)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetBacklogCards_EmptyBoard_ReturnsEmptyList()
    {
        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetBacklogCards_AllCardsUnassigned_ReturnsAll()
    {
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card 1");
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card 2");
        await TestHelpers.SeedCardAsync(_db, _doneSwimlane.Id, _adminUserId, "Card 3");

        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public async Task GetBacklogCards_SomeCardsInSprint_ExcludesAssigned()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Backlog Card");
        var card2 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Sprint Card");

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card2.Id, _admin);

        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Backlog Card", result[0].Title);
    }

    [TestMethod]
    public async Task GetBacklogCards_CardInCompletedSprint_IncludedInBacklog()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Completed Sprint Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Done Sprint");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);
        await _sprintService.StartSprintAsync(sprint.Id, _admin);
        await _sprintService.CompleteSprintAsync(sprint.Id, _admin);

        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        // Card in completed sprint should appear in backlog since GetBacklogCardsAsync
        // only excludes active/planning sprints
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Completed Sprint Card", result[0].Title);
    }

    [TestMethod]
    public async Task GetBacklogCards_ExcludesDeletedCards()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Deleted");
        card.IsDeleted = true;
        card.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetBacklogCards_ExcludesArchivedCards()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Archived");
        card.IsArchived = true;
        await _db.SaveChangesAsync();

        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetBacklogCards_ReturnsDtoWithExpectedFields()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Detailed Card");
        card.Priority = CardPriority.High;
        card.StoryPoints = 5;
        card.DueDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        await _db.SaveChangesAsync();

        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(1, result.Count);
        var dto = result[0];
        Assert.AreEqual("Detailed Card", dto.Title);
        Assert.AreEqual(CardPriority.High, dto.Priority);
        Assert.AreEqual(5, dto.StoryPoints);
        Assert.IsNotNull(dto.DueDate);
        Assert.IsNull(dto.SprintId);
    }

    [TestMethod]
    public async Task GetBacklogCards_MemberCanAccess()
    {
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card");

        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _member);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task GetBacklogCards_NonMember_Throws()
    {
        var outsider = TestHelpers.CreateCaller(Guid.NewGuid());

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _sprintService.GetBacklogCardsAsync(_teamBoard.Id, outsider));
    }

    [TestMethod]
    public async Task GetBacklogCards_OrderedByPriorityThenPosition()
    {
        var lowCard = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Low Priority");
        lowCard.Priority = CardPriority.Low;
        lowCard.Position = 1000;

        var highCard = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "High Priority");
        highCard.Priority = CardPriority.High;
        highCard.Position = 2000;

        var urgentCard = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Urgent");
        urgentCard.Priority = CardPriority.Urgent;
        urgentCard.Position = 3000;

        await _db.SaveChangesAsync();

        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("Urgent", result[0].Title);
        Assert.AreEqual("High Priority", result[1].Title);
        Assert.AreEqual("Low Priority", result[2].Title);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sprint-Filtered Card Listing (Step 28 — KanbanBoard filter)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ListCards_NoSprintFilter_ReturnsAllCards()
    {
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card A");
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card B");

        var result = await _cardService.ListCardsAsync(_todoSwimlane.Id, _admin);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task ListCards_WithSprintFilter_ReturnsOnlySprintCards()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Sprint Card");
        var card2 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Non-Sprint Card");

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card1.Id, _admin);

        var result = await _cardService.ListCardsAsync(_todoSwimlane.Id, _admin, sprintId: sprint.Id);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Sprint Card", result[0].Title);
    }

    [TestMethod]
    public async Task ListCards_WithSprintFilter_NoMatches_ReturnsEmpty()
    {
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card");

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Empty Sprint");

        var result = await _cardService.ListCardsAsync(_todoSwimlane.Id, _admin, sprintId: sprint.Id);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task ListCards_WithSprintFilter_MultipleSwimlanesHaveSprintCards()
    {
        var cardTodo = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Todo Sprint Card");
        var cardDone = await TestHelpers.SeedCardAsync(_db, _doneSwimlane.Id, _adminUserId, "Done Sprint Card");
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Not in sprint");

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, cardTodo.Id, _admin);
        await _sprintService.AddCardToSprintAsync(sprint.Id, cardDone.Id, _admin);

        var todoResult = await _cardService.ListCardsAsync(_todoSwimlane.Id, _admin, sprintId: sprint.Id);
        var doneResult = await _cardService.ListCardsAsync(_doneSwimlane.Id, _admin, sprintId: sprint.Id);

        Assert.AreEqual(1, todoResult.Count);
        Assert.AreEqual("Todo Sprint Card", todoResult[0].Title);
        Assert.AreEqual(1, doneResult.Count);
        Assert.AreEqual("Done Sprint Card", doneResult[0].Title);
    }

    [TestMethod]
    public async Task ListCards_SprintFilteredCards_HaveSprintInfo()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Sprint Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint Alpha");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);

        // When we list all cards (no filter), cards should have sprint info
        var result = await _cardService.ListCardsAsync(_todoSwimlane.Id, _admin);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(sprint.Id, result[0].SprintId);
        Assert.AreEqual("Sprint Alpha", result[0].SprintTitle);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Bulk Sprint Assignment (Step 27 — BacklogView bulk assign)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task BatchAddCards_AssignsMultipleCards()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card 1");
        var card2 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card 2");
        var card3 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card 3");

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");

        await _sprintService.BatchAddCardsAsync(sprint.Id, [card1.Id, card2.Id, card3.Id], _admin);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);
        Assert.AreEqual(3, sprintCards.Count);
    }

    [TestMethod]
    public async Task BatchAddCards_SkipsDuplicates()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card 1");
        var card2 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card 2");

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card1.Id, _admin);

        // Batch add includes card1 which is already assigned
        await _sprintService.BatchAddCardsAsync(sprint.Id, [card1.Id, card2.Id], _admin);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);
        Assert.AreEqual(2, sprintCards.Count);
    }

    [TestMethod]
    public async Task BatchAddCards_SkipsDeletedCards()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Valid");
        var card2 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Deleted");
        card2.IsDeleted = true;
        card2.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");

        await _sprintService.BatchAddCardsAsync(sprint.Id, [card1.Id, card2.Id], _admin);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);
        Assert.AreEqual(1, sprintCards.Count);
        Assert.AreEqual("Valid", sprintCards[0].Title);
    }

    [TestMethod]
    public async Task BatchAddCards_EmptyList_NoError()
    {
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");

        await _sprintService.BatchAddCardsAsync(sprint.Id, [], _admin);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);
        Assert.AreEqual(0, sprintCards.Count);
    }

    [TestMethod]
    public async Task BatchAddCards_RemovesFromBacklog()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card 1");
        var card2 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card 2");
        var card3 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card 3");

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");

        // Verify all in backlog initially
        var backlogBefore = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(3, backlogBefore.Count);

        // Assign 2 cards to sprint
        await _sprintService.BatchAddCardsAsync(sprint.Id, [card1.Id, card2.Id], _admin);

        // Only card3 should remain in backlog
        var backlogAfter = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(1, backlogAfter.Count);
        Assert.AreEqual("Card 3", backlogAfter[0].Title);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Individual Card Sprint Assignment/Removal
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AddCardToSprint_MovesFromBacklogToSprint()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "My Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");

        // Before: in backlog
        var backlogBefore = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(1, backlogBefore.Count);

        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);

        // After: not in backlog
        var backlogAfter = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(0, backlogAfter.Count);

        // In sprint
        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);
        Assert.AreEqual(1, sprintCards.Count);
        Assert.AreEqual("My Card", sprintCards[0].Title);
    }

    [TestMethod]
    public async Task RemoveCardFromSprint_ReturnsToBacklog()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "My Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);

        // Remove from sprint
        await _sprintService.RemoveCardFromSprintAsync(sprint.Id, card.Id, _admin);

        // Should be back in backlog
        var backlog = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(1, backlog.Count);
        Assert.AreEqual("My Card", backlog[0].Title);

        // No longer in sprint
        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);
        Assert.AreEqual(0, sprintCards.Count);
    }

    [TestMethod]
    public async Task AddCardToSprint_Idempotent()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");

        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);
        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);
        Assert.AreEqual(1, sprintCards.Count);
    }

    [TestMethod]
    public async Task RemoveCardFromSprint_Idempotent()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);

        await _sprintService.RemoveCardFromSprintAsync(sprint.Id, card.Id, _admin);
        await _sprintService.RemoveCardFromSprintAsync(sprint.Id, card.Id, _admin);

        // Should not throw
        var backlog = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(1, backlog.Count);
    }

    [TestMethod]
    public async Task AddCardToSprint_InvalidCard_Throws()
    {
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _sprintService.AddCardToSprintAsync(sprint.Id, Guid.NewGuid(), _admin));
    }

    [TestMethod]
    public async Task AddCardToSprint_InvalidSprint_Throws()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card");

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _sprintService.AddCardToSprintAsync(Guid.NewGuid(), card.Id, _admin));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Card in Multiple Sprints Scenario
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CardInMultipleSprints_BothReportCard()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Shared Card");
        var sprint1 = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        var sprint2 = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 2");

        await _sprintService.AddCardToSprintAsync(sprint1.Id, card.Id, _admin);
        await _sprintService.AddCardToSprintAsync(sprint2.Id, card.Id, _admin);

        var cards1 = await _sprintService.GetSprintCardsAsync(sprint1.Id, _admin);
        var cards2 = await _sprintService.GetSprintCardsAsync(sprint2.Id, _admin);

        Assert.AreEqual(1, cards1.Count);
        Assert.AreEqual(1, cards2.Count);
    }

    [TestMethod]
    public async Task CardInActiveSprint_NotInBacklog()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Active Sprint Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Active Sprint");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);
        await _sprintService.StartSprintAsync(sprint.Id, _admin);

        var backlog = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(0, backlog.Count);
    }

    [TestMethod]
    public async Task CardInPlanningSprint_NotInBacklog()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Planning Sprint Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Planning Sprint");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);

        var backlog = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(0, backlog.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Controller Endpoint Tests: Backlog (Step 16/29)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task BacklogController_ReturnsBacklogCards()
    {
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Backlog Card");
        var card2 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Sprint Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card2.Id, _admin);

        var result = await _backlogController.GetBacklogAsync(_teamBoard.Id);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsNotNull(ok.Value);
        var data = ok.Value!.GetType().GetProperty("data")?.GetValue(ok.Value);
        var cards = data as IReadOnlyList<CardDto>;
        Assert.IsNotNull(cards);
        Assert.AreEqual(1, cards.Count);
        Assert.AreEqual("Backlog Card", cards[0].Title);
    }

    [TestMethod]
    public async Task CardsController_SprintFilter_ReturnsFilteredCards()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Sprint Card");
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Other Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card1.Id, _admin);

        var result = await _cardsController.ListCardsAsync(_todoSwimlane.Id, sprintId: sprint.Id);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsNotNull(ok.Value);
        var data = ok.Value!.GetType().GetProperty("data")?.GetValue(ok.Value);
        var cards = data as IReadOnlyList<CardDto>;
        Assert.IsNotNull(cards);
        Assert.AreEqual(1, cards.Count);
        Assert.AreEqual("Sprint Card", cards[0].Title);
    }

    [TestMethod]
    public async Task CardsController_NoSprintFilter_ReturnsAll()
    {
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card A");
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card B");

        var result = await _cardsController.ListCardsAsync(_todoSwimlane.Id);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsNotNull(ok.Value);
        var data = ok.Value!.GetType().GetProperty("data")?.GetValue(ok.Value);
        var cards = data as IReadOnlyList<CardDto>;
        Assert.IsNotNull(cards);
        Assert.AreEqual(2, cards.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sprint View Tab Logic (Step 28 — Sprint-filtered Kanban)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task SprintCards_AcrossSwimlanes_AllReturnedForSprint()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Todo Card");
        var card2 = await TestHelpers.SeedCardAsync(_db, _doneSwimlane.Id, _adminUserId, "Done Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card1.Id, _admin);
        await _sprintService.AddCardToSprintAsync(sprint.Id, card2.Id, _admin);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);

        Assert.AreEqual(2, sprintCards.Count);
        Assert.IsTrue(sprintCards.Any(c => c.Title == "Todo Card"));
        Assert.IsTrue(sprintCards.Any(c => c.Title == "Done Card"));
    }

    [TestMethod]
    public async Task SprintCards_HaveCorrectSprintInfo()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint Alpha");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);

        Assert.AreEqual(1, sprintCards.Count);
        Assert.AreEqual(sprint.Id, sprintCards[0].SprintId);
        Assert.AreEqual("Sprint Alpha", sprintCards[0].SprintTitle);
    }

    [TestMethod]
    public async Task SprintCards_ExcludesDeletedCards()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Good Card");
        var card2 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Deleted Card");

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card1.Id, _admin);
        await _sprintService.AddCardToSprintAsync(sprint.Id, card2.Id, _admin);

        card2.IsDeleted = true;
        card2.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);

        Assert.AreEqual(1, sprintCards.Count);
        Assert.AreEqual("Good Card", sprintCards[0].Title);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sprint Card Aggregate Stats (Step 27 — BacklogView stats)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task Sprint_CardCount_Accurate()
    {
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "C1");
        var card2 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "C2");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card1.Id, _admin);
        await _sprintService.AddCardToSprintAsync(sprint.Id, card2.Id, _admin);

        var sprintDto = await _sprintService.GetSprintAsync(sprint.Id, _admin);

        Assert.IsNotNull(sprintDto);
        Assert.AreEqual(2, sprintDto.CardCount);
    }

    [TestMethod]
    public async Task Sprint_StoryPoints_SummedCorrectly()
    {
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "C1");
        card1.StoryPoints = 3;
        var card2 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "C2");
        card2.StoryPoints = 5;
        var card3 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "C3");
        // No story points
        await _db.SaveChangesAsync();

        await _sprintService.AddCardToSprintAsync(sprint.Id, card1.Id, _admin);
        await _sprintService.AddCardToSprintAsync(sprint.Id, card2.Id, _admin);
        await _sprintService.AddCardToSprintAsync(sprint.Id, card3.Id, _admin);

        var sprintDto = await _sprintService.GetSprintAsync(sprint.Id, _admin);

        Assert.IsNotNull(sprintDto);
        Assert.AreEqual(8, sprintDto.TotalStoryPoints);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Mode Guards — Backlog only for Team boards (Step 29)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task PersonalBoard_SprintCreation_Blocked()
    {
        var personalCaller = TestHelpers.CreateCaller(_adminUserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _personalBoard.Id, "Todo");
        await TestHelpers.SeedCardAsync(_db, swimlane.Id, _adminUserId, "Card");

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _sprintPlanningService.CreateYearPlanAsync(
                _personalBoard.Id,
                new CreateSprintPlanDto { StartDate = DateTime.UtcNow, SprintCount = 1, DefaultDurationWeeks = 2 },
                personalCaller));
    }

    [TestMethod]
    public async Task TeamBoard_BacklogAccess_Succeeds()
    {
        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        // Should not throw, returns empty list for new board
        Assert.IsNotNull(result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sprint Lifecycle Affects Backlog (Sprint complete → cards return)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CompleteSprint_IncompleteCards_ReappearInBacklog()
    {
        var card1 = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Incomplete");
        var card2 = await TestHelpers.SeedCardAsync(_db, _doneSwimlane.Id, _adminUserId, "Done Card");

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card1.Id, _admin);
        await _sprintService.AddCardToSprintAsync(sprint.Id, card2.Id, _admin);
        await _sprintService.StartSprintAsync(sprint.Id, _admin);
        await _sprintService.CompleteSprintAsync(sprint.Id, _admin);

        // Both should now appear in backlog since they're only in a completed sprint
        var backlog = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(2, backlog.Count);
    }

    [TestMethod]
    public async Task ActiveSprint_CardStillFilteredInSwimlane()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Active Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Active Sprint");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);
        await _sprintService.StartSprintAsync(sprint.Id, _admin);

        // Card should be filterable by sprint
        var filtered = await _cardService.ListCardsAsync(_todoSwimlane.Id, _admin, sprintId: sprint.Id);
        Assert.AreEqual(1, filtered.Count);

        // And not in backlog
        var backlog = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(0, backlog.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Large Dataset Tests (Step 27 — many cards)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task Backlog_ManyCards_AllReturned()
    {
        for (var i = 0; i < 50; i++)
        {
            await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, $"Card {i}");
        }

        var result = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(50, result.Count);
    }

    [TestMethod]
    public async Task BatchAssign_ManyCards_Succeeds()
    {
        var cardIds = new List<Guid>();
        for (var i = 0; i < 20; i++)
        {
            var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, $"Card {i}");
            cardIds.Add(card.Id);
        }

        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");

        await _sprintService.BatchAddCardsAsync(sprint.Id, cardIds, _admin);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);
        Assert.AreEqual(20, sprintCards.Count);

        var backlog = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(0, backlog.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Member Role Tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task Member_CanViewBacklog()
    {
        await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card");

        var backlog = await _sprintService.GetBacklogCardsAsync(_teamBoard.Id, _member);

        Assert.AreEqual(1, backlog.Count);
    }

    [TestMethod]
    public async Task Member_CanAddCardToSprint()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");

        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _member);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _member);
        Assert.AreEqual(1, sprintCards.Count);
    }

    [TestMethod]
    public async Task Member_CanRemoveCardFromSprint()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");
        await _sprintService.AddCardToSprintAsync(sprint.Id, card.Id, _admin);

        await _sprintService.RemoveCardFromSprintAsync(sprint.Id, card.Id, _member);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);
        Assert.AreEqual(0, sprintCards.Count);
    }

    [TestMethod]
    public async Task Member_CanBatchAssign()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _todoSwimlane.Id, _adminUserId, "Card");
        var sprint = await CreateTestSprintAsync(_teamBoard.Id, "Sprint 1");

        await _sprintService.BatchAddCardsAsync(sprint.Id, [card.Id], _member);

        var sprintCards = await _sprintService.GetSprintCardsAsync(sprint.Id, _admin);
        Assert.AreEqual(1, sprintCards.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private async Task<SprintDto> CreateTestSprintAsync(Guid boardId, string title)
    {
        var sprint = await _sprintService.CreateSprintAsync(boardId, new CreateSprintDto
        {
            Title = title,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14)
        }, _admin);
        return sprint!;
    }

    private static void SetupControllerContext(ControllerBase controller, Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "user")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
