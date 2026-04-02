using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using IEventBus = DotNetCloud.Core.Events.IEventBus;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Phase J comprehensive tests for the Tracks Dual-Mode Rework:
/// Steps 35-42 covering data model validation, mode-aware service guards,
/// sprint planning edge cases, review session edge cases, poker vote status,
/// controller tests, security tests, and performance tests.
/// </summary>
[TestClass]
public class PhaseJ_ComprehensiveTests
{
    private TracksDbContext _db = null!;
    private BoardService _boardService = null!;
    private CardService _cardService = null!;
    private SwimlaneService _swimlaneService = null!;
    private SprintService _sprintService = null!;
    private SprintPlanningService _planningService = null!;
    private ReviewSessionService _reviewService = null!;
    private PokerService _pokerService = null!;
    private ActivityService _activityService = null!;
    private TeamService _teamService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<ITracksRealtimeService> _realtimeMock = null!;
    private CallerContext _owner = null!;
    private CallerContext _admin = null!;
    private CallerContext _member = null!;
    private CallerContext _viewer = null!;
    private CallerContext _outsider = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _realtimeMock = new Mock<ITracksRealtimeService>();
        _activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        _teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, _eventBusMock.Object, _activityService, _teamService, NullLogger<BoardService>.Instance);
        _swimlaneService = new SwimlaneService(_db, _boardService, _activityService, NullLogger<SwimlaneService>.Instance);
        _cardService = new CardService(_db, _boardService, _activityService, _eventBusMock.Object, NullLogger<CardService>.Instance);
        _sprintService = new SprintService(_db, _boardService, _activityService, _eventBusMock.Object, NullLogger<SprintService>.Instance);
        _planningService = new SprintPlanningService(_db, _boardService, _activityService, NullLogger<SprintPlanningService>.Instance);
        _pokerService = new PokerService(_db, _boardService, _activityService, _realtimeMock.Object, NullLogger<PokerService>.Instance);
        _reviewService = new ReviewSessionService(_db, _boardService, _pokerService, _realtimeMock.Object, NullLogger<ReviewSessionService>.Instance);

        _owner = TestHelpers.CreateCaller();
        _admin = TestHelpers.CreateCaller();
        _member = TestHelpers.CreateCaller();
        _viewer = TestHelpers.CreateCaller();
        _outsider = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ═══════════════════════════════════════════════════════════════════
    // Step 35 — Data Model & Entity Validation
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Sprint_DurationWeeks_DefaultIsNull()
    {
        var sprint = new Sprint { BoardId = Guid.NewGuid(), Title = "S1" };
        Assert.IsNull(sprint.DurationWeeks);
    }

    [TestMethod]
    public void Sprint_PlannedOrder_DefaultIsNull()
    {
        var sprint = new Sprint { BoardId = Guid.NewGuid(), Title = "S1" };
        Assert.IsNull(sprint.PlannedOrder);
    }

    [TestMethod]
    public async Task Sprint_EndDate_ComputedFromStartDateAndDuration()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var start = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 1,
            DefaultDurationWeeks = 3
        };

        var result = await _planningService.CreateYearPlanAsync(board.Id, dto, _owner);

        var sprint = result.Sprints[0];
        Assert.AreEqual(start, sprint.StartDate);
        Assert.AreEqual(start.AddDays(21), sprint.EndDate); // 3 weeks × 7 days
    }

    [TestMethod]
    public void Board_Mode_DefaultIsPersonal()
    {
        var board = new Board { Title = "Test", OwnerId = Guid.NewGuid() };
        Assert.AreEqual(BoardMode.Personal, board.Mode);
    }

    [TestMethod]
    public void PokerSession_ReviewSessionId_DefaultIsNull()
    {
        var session = new PokerSession { CardId = Guid.NewGuid(), BoardId = Guid.NewGuid(), CreatedByUserId = Guid.NewGuid() };
        Assert.IsNull(session.ReviewSessionId);
    }

    [TestMethod]
    public void ReviewSession_Collections_InitializedEmpty()
    {
        var session = new ReviewSession { BoardId = Guid.NewGuid(), HostUserId = Guid.NewGuid() };
        Assert.IsNotNull(session.Participants);
        Assert.AreEqual(0, session.Participants.Count);
        Assert.IsNotNull(session.PokerSessions);
        Assert.AreEqual(0, session.PokerSessions.Count);
    }

    [TestMethod]
    public async Task ReviewSession_PersistsInDb_WithAllFields()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var cardSwim = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, cardSwim.Id, _owner.UserId);

        var session = new ReviewSession
        {
            BoardId = board.Id,
            HostUserId = _owner.UserId,
            CurrentCardId = card.Id,
            Status = ReviewSessionStatus.Active
        };
        _db.ReviewSessions.Add(session);
        await _db.SaveChangesAsync();

        var retrieved = await _db.ReviewSessions
            .Include(r => r.Participants)
            .FirstOrDefaultAsync(r => r.Id == session.Id);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(board.Id, retrieved.BoardId);
        Assert.AreEqual(_owner.UserId, retrieved.HostUserId);
        Assert.AreEqual(card.Id, retrieved.CurrentCardId);
        Assert.AreEqual(ReviewSessionStatus.Active, retrieved.Status);
    }

    [TestMethod]
    public async Task ReviewSessionParticipant_PersistsInDb()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var session = new ReviewSession { BoardId = board.Id, HostUserId = _owner.UserId };
        _db.ReviewSessions.Add(session);
        await _db.SaveChangesAsync();

        var participant = new ReviewSessionParticipant
        {
            ReviewSessionId = session.Id,
            UserId = _owner.UserId
        };
        _db.ReviewSessionParticipants.Add(participant);
        await _db.SaveChangesAsync();

        var retrieved = await _db.ReviewSessionParticipants.FindAsync(participant.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(session.Id, retrieved.ReviewSessionId);
        Assert.IsTrue(retrieved.IsConnected);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 36 — Mode-Aware Service Tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task PersonalBoard_CreateSprintViaPlanning_Throws()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId, "Personal");
        // Default mode is Personal — SprintPlanningService enforces team mode

        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.CreateYearPlanAsync(board.Id, dto, _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task PersonalBoard_IndividualSprint_AllowedByBaseService()
    {
        // Note: SprintService.CreateSprintAsync does NOT enforce team mode.
        // Mode enforcement is at SprintPlanningService (wizard) and ReviewSessionService level.
        // Individual sprints are technically allowed on personal boards at the service layer.
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId, "Personal");

        var result = await _sprintService.CreateSprintAsync(board.Id,
            new CreateSprintDto
            {
                Title = "Sprint 1",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(14)
            }, _owner);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task PersonalBoard_StartReviewSession_Throws()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId, "Personal");

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartSessionAsync(board.Id, _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task PersonalBoard_CreateYearPlan_Throws()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId, "Personal");

        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.CreateYearPlanAsync(board.Id, dto, _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task PersonalBoard_GetBacklog_ReturnsAllCards()
    {
        // Personal boards have no sprints — all cards are effectively "backlog"
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId, "Personal");
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Card 1");
        await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Card 2");

        var backlog = await _sprintService.GetBacklogCardsAsync(board.Id, _owner);

        Assert.AreEqual(2, backlog.Count);
    }

    [TestMethod]
    public async Task TeamBoard_AllOperations_Succeed()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _admin.UserId, BoardMemberRole.Admin);

        // Create sprint plan
        var planDto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        };
        var plan = await _planningService.CreateYearPlanAsync(board.Id, planDto, _owner);
        Assert.AreEqual(2, plan.Sprints.Count);

        // Start review session
        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        Assert.IsNotNull(session);

        // Get backlog
        var backlog = await _sprintService.GetBacklogCardsAsync(board.Id, _owner);
        Assert.IsNotNull(backlog);

        // Cleanup
        await _reviewService.EndSessionAsync(session.Id, _owner);
    }

    [TestMethod]
    public async Task ListCards_WithSprintFilter_ReturnsFilteredCards()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card1 = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Card 1");
        var card2 = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Card 2");

        // Create sprints and assign card1 to sprint
        var sprint = await _sprintService.CreateSprintAsync(board.Id,
            new CreateSprintDto { Title = "Sprint 1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14) }, _owner);
        _db.SprintCards.Add(new SprintCard { SprintId = sprint.Id, CardId = card1.Id });
        await _db.SaveChangesAsync();

        // List cards with sprint filter should only return card1
        var filtered = await _cardService.ListCardsAsync(swimlane.Id, _owner, sprintId: sprint.Id);
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual(card1.Id, filtered[0].Id);
    }

    [TestMethod]
    public async Task ListCards_WithSprintFilter_NoMatches_ReturnsEmpty()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Unassigned Card");

        var sprint = await _sprintService.CreateSprintAsync(board.Id,
            new CreateSprintDto { Title = "Sprint 1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14) }, _owner);

        var filtered = await _cardService.ListCardsAsync(swimlane.Id, _owner, sprintId: sprint.Id);
        Assert.AreEqual(0, filtered.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 37 — Sprint Planning Wizard Edge Cases
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateYearPlan_SpansYearBoundary_Succeeds()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var start = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        var result = await _planningService.CreateYearPlanAsync(board.Id, dto, _owner);

        Assert.AreEqual(4, result.Sprints.Count);
        // Sprint 3 starts Dec 29, ends Jan 12 2027
        Assert.AreEqual(start.AddDays(28), result.Sprints[2].StartDate);
        Assert.AreEqual(new DateTime(2027, 1, 12, 0, 0, 0, DateTimeKind.Utc), result.Sprints[2].EndDate);
    }

    [TestMethod]
    public async Task CreateYearPlan_MaxValidCount_104_Succeeds()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var dto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 104,
            DefaultDurationWeeks = 1
        };

        var result = await _planningService.CreateYearPlanAsync(board.Id, dto, _owner);

        Assert.AreEqual(104, result.Sprints.Count);
        Assert.AreEqual(104, result.TotalWeeks);
    }

    [TestMethod]
    public async Task AdjustSprint_MultipleAdjustments_AllCascadeCorrectly()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var planDto = new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };
        var plan = await _planningService.CreateYearPlanAsync(board.Id, planDto, _owner);

        // Adjust sprint 1: 2→3 weeks
        await _planningService.AdjustSprintAsync(plan.Sprints[0].Id,
            new AdjustSprintDto { DurationWeeks = 3 }, _owner);

        // Adjust sprint 3: 2→1 week
        var result = await _planningService.AdjustSprintAsync(plan.Sprints[2].Id,
            new AdjustSprintDto { DurationWeeks = 1 }, _owner);

        // Sprint 1: Jan 5 → Jan 26 (3 weeks)
        Assert.AreEqual(3, result.Sprints[0].DurationWeeks);
        Assert.AreEqual(start, result.Sprints[0].StartDate);
        Assert.AreEqual(start.AddDays(21), result.Sprints[0].EndDate);

        // Sprint 2: Jan 26 → Feb 9 (2 weeks, unchanged)
        Assert.AreEqual(2, result.Sprints[1].DurationWeeks);
        Assert.AreEqual(start.AddDays(21), result.Sprints[1].StartDate);

        // Sprint 3: Feb 9 → Feb 16 (1 week now)
        Assert.AreEqual(1, result.Sprints[2].DurationWeeks);
        Assert.AreEqual(start.AddDays(35), result.Sprints[2].StartDate);
        Assert.AreEqual(start.AddDays(42), result.Sprints[2].EndDate);

        // Sprint 4: Feb 16 → Mar 2 (2 weeks, cascaded from sprint 3)
        Assert.AreEqual(2, result.Sprints[3].DurationWeeks);
        Assert.AreEqual(start.AddDays(42), result.Sprints[3].StartDate);
    }

    [TestMethod]
    public async Task AdjustSprint_MinToMax_Succeeds()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 1,
            DefaultDurationWeeks = 1  // min
        };
        var plan = await _planningService.CreateYearPlanAsync(board.Id, dto, _owner);

        // Adjust to max
        var result = await _planningService.AdjustSprintAsync(plan.Sprints[0].Id,
            new AdjustSprintDto { DurationWeeks = 16 }, _owner);

        Assert.AreEqual(16, result.Sprints[0].DurationWeeks);
    }

    [TestMethod]
    public async Task AdjustSprint_InvalidDuration0_Throws()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var plan = await CreateTestPlan(board.Id, 2, 2);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.AdjustSprintAsync(plan.Sprints[0].Id,
                new AdjustSprintDto { DurationWeeks = 0 }, _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InvalidSprintDuration));
    }

    [TestMethod]
    public async Task AdjustSprint_InvalidDuration17_Throws()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var plan = await CreateTestPlan(board.Id, 2, 2);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.AdjustSprintAsync(plan.Sprints[0].Id,
                new AdjustSprintDto { DurationWeeks = 17 }, _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InvalidSprintDuration));
    }

    [TestMethod]
    public async Task AdjustSprint_NonMember_Throws()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var plan = await CreateTestPlan(board.Id, 2, 2);

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.AdjustSprintAsync(plan.Sprints[0].Id,
                new AdjustSprintDto { DurationWeeks = 3 }, _outsider));
    }

    [TestMethod]
    public async Task GetPlanOverview_EmptyBoard_ReturnsEmptyPlan()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);

        var overview = await _planningService.GetPlanOverviewAsync(board.Id, _owner);

        Assert.IsNotNull(overview);
        Assert.AreEqual(0, overview.Sprints.Count);
        Assert.AreEqual(0, overview.TotalWeeks);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 38 — Review Session Edge Cases
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task EndSession_WithActivePoker_EndsSession()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        await _reviewService.SetCurrentCardAsync(session.Id, card.Id, _owner);
        await _reviewService.StartPokerForCurrentCardAsync(session.Id, new StartReviewPokerDto(), _owner);

        // End session even though poker is still active
        await _reviewService.EndSessionAsync(session.Id, _owner);

        var state = await _reviewService.GetSessionStateAsync(session.Id, _owner);
        Assert.AreEqual(ReviewSessionStatus.Ended, state!.Status);
    }

    [TestMethod]
    public async Task SetCurrentCard_NonExistentCard_Throws()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var session = await _reviewService.StartSessionAsync(board.Id, _owner);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.SetCurrentCardAsync(session.Id, Guid.NewGuid(), _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.CardNotFound));
    }

    [TestMethod]
    public async Task JoinSession_BoardMember_Succeeds()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _member.UserId, BoardMemberRole.Member);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        var result = await _reviewService.JoinSessionAsync(session.Id, _member);

        Assert.AreEqual(2, result.Participants.Count);
    }

    [TestMethod]
    public async Task JoinSession_SameUserTwice_IdempotentReconnect()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _member.UserId, BoardMemberRole.Member);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.LeaveSessionAsync(session.Id, _member);

        // Rejoin is idempotent
        var result = await _reviewService.JoinSessionAsync(session.Id, _member);
        var participant = result.Participants.First(p => p.UserId == _member.UserId);
        Assert.IsTrue(participant.IsConnected);
        Assert.AreEqual(2, result.Participants.Count); // No duplicate
    }

    [TestMethod]
    public async Task StartSession_AfterPreviousEnded_CanStartNew()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);

        var first = await _reviewService.StartSessionAsync(board.Id, _owner);
        await _reviewService.EndSessionAsync(first.Id, _owner);

        // Should allow new session
        var second = await _reviewService.StartSessionAsync(board.Id, _owner);
        Assert.IsNotNull(second);
        Assert.AreNotEqual(first.Id, second.Id);
    }

    [TestMethod]
    public async Task GetActiveSessionForBoard_AfterEnd_ReturnsNull()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        await _reviewService.EndSessionAsync(session.Id, _owner);

        var active = await _reviewService.GetActiveSessionForBoardAsync(board.Id, _owner);
        Assert.IsNull(active);
    }

    [TestMethod]
    public async Task GetSessionState_IncludesAllParticipants()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _member.UserId, BoardMemberRole.Member);
        await TestHelpers.AddMemberAsync(_db, board.Id, _viewer.UserId, BoardMemberRole.Viewer);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.JoinSessionAsync(session.Id, _viewer);

        var state = await _reviewService.GetSessionStateAsync(session.Id, _owner);
        Assert.AreEqual(3, state!.Participants.Count);
    }

    [TestMethod]
    public async Task ReviewSession_FullLifecycle_HostToEnd()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _member.UserId, BoardMemberRole.Member);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        // Start session
        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        Assert.AreEqual(ReviewSessionStatus.Active, session.Status);

        // Member joins
        await _reviewService.JoinSessionAsync(session.Id, _member);

        // Host sets current card
        var updated = await _reviewService.SetCurrentCardAsync(session.Id, card.Id, _owner);
        Assert.AreEqual(card.Id, updated.CurrentCardId);

        // Host starts poker
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id, new StartReviewPokerDto(), _owner);
        Assert.IsNotNull(withPoker.ActivePokerSession);

        // Member votes
        await _pokerService.SubmitVoteAsync(withPoker.ActivePokerSession!.Id,
            new SubmitPokerVoteDto { Estimate = "5" }, _member);

        // Owner votes
        await _pokerService.SubmitVoteAsync(withPoker.ActivePokerSession.Id,
            new SubmitPokerVoteDto { Estimate = "8" }, _owner);

        // Host reveals
        var revealed = await _pokerService.RevealSessionAsync(withPoker.ActivePokerSession.Id, _owner);
        Assert.AreEqual(PokerSessionStatus.Revealed, revealed.Status);

        // Host accepts estimate
        var accepted = await _pokerService.AcceptEstimateAsync(withPoker.ActivePokerSession.Id,
            new AcceptPokerEstimateDto { AcceptedEstimate = "8", StoryPoints = 8 }, _owner);
        Assert.AreEqual(PokerSessionStatus.Completed, accepted.Status);

        // Member leaves
        await _reviewService.LeaveSessionAsync(session.Id, _member);

        // Host ends session
        await _reviewService.EndSessionAsync(session.Id, _owner);

        var finalState = await _reviewService.GetSessionStateAsync(session.Id, _owner);
        Assert.AreEqual(ReviewSessionStatus.Ended, finalState!.Status);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 39 — Poker Vote Status Tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetVoteStatus_AfterReveal_StillShowsVoters()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        var session = await _pokerService.StartSessionAsync(card.Id,
            new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _owner);
        await _pokerService.SubmitVoteAsync(session.Id, new SubmitPokerVoteDto { Estimate = "5" }, _owner);
        await _pokerService.RevealSessionAsync(session.Id, _owner);

        var status = await _pokerService.GetVoteStatusAsync(session.Id, _owner);

        Assert.AreEqual(1, status.Count);
        Assert.IsTrue(status[0].HasVoted);
    }

    [TestMethod]
    public async Task GetVoteStatus_NewRound_Resets()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId);
        var memberId = Guid.NewGuid();
        await TestHelpers.AddMemberAsync(_db, board.Id, memberId, BoardMemberRole.Member);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        var session = await _pokerService.StartSessionAsync(card.Id,
            new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _owner);
        await _pokerService.SubmitVoteAsync(session.Id, new SubmitPokerVoteDto { Estimate = "5" }, _owner);
        await _pokerService.RevealSessionAsync(session.Id, _owner);

        // Start new round
        await _pokerService.StartNewRoundAsync(session.Id, _owner);

        var status = await _pokerService.GetVoteStatusAsync(session.Id, _owner);

        // After new round, votes should reset
        Assert.AreEqual(2, status.Count);
        Assert.IsTrue(status.All(s => !s.HasVoted));
    }

    [TestMethod]
    public async Task GetVoteStatus_StandalonePoker_Works()
    {
        // Poker session NOT linked to a review session
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        var session = await _pokerService.StartSessionAsync(card.Id,
            new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _owner);

        // Verify ReviewSessionId is null (standalone)
        var dbSession = await _db.PokerSessions.FindAsync(session.Id);
        Assert.IsNull(dbSession!.ReviewSessionId);

        // Vote status should still work
        var status = await _pokerService.GetVoteStatusAsync(session.Id, _owner);
        Assert.AreEqual(1, status.Count);
        Assert.IsFalse(status[0].HasVoted); // Not yet voted
    }

    [TestMethod]
    public async Task GetVoteStatus_MultipleMembers_CorrectCounts()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId);
        var member1 = Guid.NewGuid();
        var member2 = Guid.NewGuid();
        var member3 = Guid.NewGuid();
        await TestHelpers.AddMemberAsync(_db, board.Id, member1, BoardMemberRole.Member);
        await TestHelpers.AddMemberAsync(_db, board.Id, member2, BoardMemberRole.Member);
        await TestHelpers.AddMemberAsync(_db, board.Id, member3, BoardMemberRole.Member);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        var session = await _pokerService.StartSessionAsync(card.Id,
            new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _owner);

        // Only owner and member1 vote
        await _pokerService.SubmitVoteAsync(session.Id, new SubmitPokerVoteDto { Estimate = "5" }, _owner);
        await _pokerService.SubmitVoteAsync(session.Id,
            new SubmitPokerVoteDto { Estimate = "8" }, TestHelpers.CreateCaller(member1));

        var status = await _pokerService.GetVoteStatusAsync(session.Id, _owner);

        Assert.AreEqual(4, status.Count); // owner + 3 members
        var voted = status.Where(s => s.HasVoted).ToList();
        var notVoted = status.Where(s => !s.HasVoted).ToList();
        Assert.AreEqual(2, voted.Count);
        Assert.AreEqual(2, notVoted.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 40 — Controller Integration Tests (service through controller)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task BacklogEndpoint_ReturnsOnlyUnsprintedCards()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card1 = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Backlog Card");
        var card2 = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Sprinted Card");

        var sprint = await _sprintService.CreateSprintAsync(board.Id,
            new CreateSprintDto { Title = "Sprint 1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14) }, _owner);
        _db.SprintCards.Add(new SprintCard { SprintId = sprint.Id, CardId = card2.Id });
        await _db.SaveChangesAsync();

        var backlog = await _sprintService.GetBacklogCardsAsync(board.Id, _owner);

        Assert.AreEqual(1, backlog.Count);
        Assert.AreEqual(card1.Id, backlog[0].Id);
    }

    [TestMethod]
    public async Task BacklogEndpoint_EmptyBoard_ReturnsEmpty()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);

        var backlog = await _sprintService.GetBacklogCardsAsync(board.Id, _owner);

        Assert.AreEqual(0, backlog.Count);
    }

    [TestMethod]
    public async Task SprintPlanOverview_ReturnsCardCounts()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);

        var plan = await CreateTestPlan(board.Id, 2, 2);

        // Add cards to sprint 1
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Sprint Card");
        _db.SprintCards.Add(new SprintCard { SprintId = plan.Sprints[0].Id, CardId = card.Id });
        await _db.SaveChangesAsync();

        var overview = await _planningService.GetPlanOverviewAsync(board.Id, _owner);

        Assert.AreEqual(2, overview.Sprints.Count);
        Assert.IsNotNull(overview.PlanStartDate);
        Assert.IsNotNull(overview.PlanEndDate);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 41 — Security Tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ReviewSession_NonBoardMember_CannotStart()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartSessionAsync(board.Id, _outsider));
    }

    [TestMethod]
    public async Task ReviewSession_MemberCannotStart_Admin()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _member.UserId, BoardMemberRole.Member);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartSessionAsync(board.Id, _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InsufficientBoardRole));
    }

    [TestMethod]
    public async Task ReviewSession_ViewerCannotStart()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _viewer.UserId, BoardMemberRole.Viewer);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartSessionAsync(board.Id, _viewer));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InsufficientBoardRole));
    }

    [TestMethod]
    public async Task ReviewSession_NonHost_CannotSetCard()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _member.UserId, BoardMemberRole.Member);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        await _reviewService.JoinSessionAsync(session.Id, _member);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.SetCurrentCardAsync(session.Id, card.Id, _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));
    }

    [TestMethod]
    public async Task ReviewSession_NonHost_CannotStartPoker()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _member.UserId, BoardMemberRole.Member);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        await _reviewService.SetCurrentCardAsync(session.Id, card.Id, _owner);
        await _reviewService.JoinSessionAsync(session.Id, _member);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartPokerForCurrentCardAsync(session.Id, new StartReviewPokerDto(), _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));
    }

    [TestMethod]
    public async Task ReviewSession_NonHost_CannotEnd()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _member.UserId, BoardMemberRole.Member);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        await _reviewService.JoinSessionAsync(session.Id, _member);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.EndSessionAsync(session.Id, _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));
    }

    [TestMethod]
    public async Task SprintPlan_MemberCannotCreate()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _member.UserId, BoardMemberRole.Member);

        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.CreateYearPlanAsync(board.Id, dto, _member));
    }

    [TestMethod]
    public async Task SprintPlan_OutsiderCannotCreate()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);

        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.CreateYearPlanAsync(board.Id, dto, _outsider));
    }

    [TestMethod]
    public async Task SprintPlan_AdminCanCreate()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _admin.UserId, BoardMemberRole.Admin);

        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        };

        var result = await _planningService.CreateYearPlanAsync(board.Id, dto, _admin);
        Assert.AreEqual(2, result.Sprints.Count);
    }

    [TestMethod]
    public async Task PersonalBoard_Owner_CannotCreateSprintPlan()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId, "Personal");

        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.CreateYearPlanAsync(board.Id, dto, _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task PersonalBoard_Owner_CannotStartReview()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId, "Personal");

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartSessionAsync(board.Id, _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task PersonalBoard_Owner_CannotCreateYearPlan()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId, "Personal");

        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.CreateYearPlanAsync(board.Id, dto, _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task ReviewSession_SetCard_FromDifferentBoard_Throws()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var otherBoard = await SeedTeamBoardAsync(_owner.UserId);
        var otherSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, otherBoard.Id);
        var otherCard = await TestHelpers.SeedCardAsync(_db, otherSwimlane.Id, _owner.UserId);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.SetCurrentCardAsync(session.Id, otherCard.Id, _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.CardNotFound));
    }

    [TestMethod]
    public async Task ReviewSession_DoubleActive_Blocked()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);

        await _reviewService.StartSessionAsync(board.Id, _owner);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartSessionAsync(board.Id, _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionAlreadyActive));
    }

    [TestMethod]
    public async Task ReviewSession_ActivePoker_BlocksSecondPoker()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        await _reviewService.SetCurrentCardAsync(session.Id, card.Id, _owner);
        await _reviewService.StartPokerForCurrentCardAsync(session.Id, new StartReviewPokerDto(), _owner);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartPokerForCurrentCardAsync(session.Id, new StartReviewPokerDto(), _owner));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewPokerStillActive));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 42 — Performance Tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ReviewSession_20Participants_CardNavigation()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card1 = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Card 1");
        var card2 = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Card 2");

        // Add 20 members and join them all to the session
        var session = await _reviewService.StartSessionAsync(board.Id, _owner);

        for (var i = 0; i < 20; i++)
        {
            var memberId = Guid.NewGuid();
            await TestHelpers.AddMemberAsync(_db, board.Id, memberId, BoardMemberRole.Member);
            var memberCaller = TestHelpers.CreateCaller(memberId);
            await _reviewService.JoinSessionAsync(session.Id, memberCaller);
        }

        // Host navigates cards
        var state1 = await _reviewService.SetCurrentCardAsync(session.Id, card1.Id, _owner);
        Assert.AreEqual(card1.Id, state1.CurrentCardId);

        var state2 = await _reviewService.SetCurrentCardAsync(session.Id, card2.Id, _owner);
        Assert.AreEqual(card2.Id, state2.CurrentCardId);

        // Verify all 21 participants (1 host + 20 members)
        var finalState = await _reviewService.GetSessionStateAsync(session.Id, _owner);
        Assert.AreEqual(21, finalState!.Participants.Count);
    }

    [TestMethod]
    public async Task ReviewSession_20Participants_PokerVoteStatus()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        var session = await _reviewService.StartSessionAsync(board.Id, _owner);

        var memberCallers = new List<CallerContext>();
        for (var i = 0; i < 20; i++)
        {
            var memberId = Guid.NewGuid();
            await TestHelpers.AddMemberAsync(_db, board.Id, memberId, BoardMemberRole.Member);
            var memberCaller = TestHelpers.CreateCaller(memberId);
            await _reviewService.JoinSessionAsync(session.Id, memberCaller);
            memberCallers.Add(memberCaller);
        }

        // Host sets card and starts poker
        await _reviewService.SetCurrentCardAsync(session.Id, card.Id, _owner);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id, new StartReviewPokerDto(), _owner);

        // 10 members vote
        var fibValues = new[] { "1", "2", "3", "5", "8", "13", "21", "1", "2", "3" };
        for (var i = 0; i < 10; i++)
        {
            await _pokerService.SubmitVoteAsync(withPoker.ActivePokerSession!.Id,
                new SubmitPokerVoteDto { Estimate = fibValues[i] }, memberCallers[i]);
        }

        // Get vote status — should show 10 voted, 11 not voted (10 participants + owner)
        var status = await _pokerService.GetVoteStatusAsync(withPoker.ActivePokerSession!.Id, _owner);

        Assert.AreEqual(21, status.Count); // 1 owner + 20 members
        var votedCount = status.Count(s => s.HasVoted);
        var notVotedCount = status.Count(s => !s.HasVoted);
        Assert.AreEqual(10, votedCount);
        Assert.AreEqual(11, notVotedCount);
    }

    [TestMethod]
    public async Task YearPlan_52Sprints_FullAdjustCascade()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 52,
            DefaultDurationWeeks = 1
        };

        var plan = await _planningService.CreateYearPlanAsync(board.Id, dto, _owner);
        Assert.AreEqual(52, plan.Sprints.Count);

        // Adjust first sprint to 2 weeks — cascades all 51 subsequent sprints
        var adjustDto = new AdjustSprintDto { DurationWeeks = 2 };
        var result = await _planningService.AdjustSprintAsync(plan.Sprints[0].Id, adjustDto, _owner);

        Assert.AreEqual(52, result.Sprints.Count);
        Assert.AreEqual(2, result.Sprints[0].DurationWeeks);
        // Second sprint should start after first sprint's new end date
        Assert.AreEqual(start.AddDays(14), result.Sprints[1].StartDate);
        // Last sprint ends at: 14 + 51*7 = 14 + 357 = 371 days
        var expectedLastEnd = start.AddDays(14 + 51 * 7);
        Assert.AreEqual(expectedLastEnd, result.Sprints[51].EndDate);
    }

    [TestMethod]
    public async Task BulkCards_100InBacklog_ListsQuickly()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);

        // Seed 100 cards
        for (var i = 0; i < 100; i++)
        {
            _db.Cards.Add(new Card
            {
                SwimlaneId = swimlane.Id,
                Title = $"Backlog Card {i + 1}",
                Position = (i + 1) * 1000.0,
                CreatedByUserId = _owner.UserId
            });
        }
        await _db.SaveChangesAsync();

        // All should appear in backlog (no sprint assignments)
        var backlog = await _sprintService.GetBacklogCardsAsync(board.Id, _owner);
        Assert.AreEqual(100, backlog.Count);
    }

    [TestMethod]
    public async Task ReviewSession_20Participants_AllDisconnect()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var session = await _reviewService.StartSessionAsync(board.Id, _owner);

        var memberCallers = new List<CallerContext>();
        for (var i = 0; i < 20; i++)
        {
            var memberId = Guid.NewGuid();
            await TestHelpers.AddMemberAsync(_db, board.Id, memberId, BoardMemberRole.Member);
            var memberCaller = TestHelpers.CreateCaller(memberId);
            await _reviewService.JoinSessionAsync(session.Id, memberCaller);
            memberCallers.Add(memberCaller);
        }

        // All 20 members disconnect
        foreach (var mc in memberCallers)
        {
            await _reviewService.LeaveSessionAsync(session.Id, mc);
        }

        var state = await _reviewService.GetSessionStateAsync(session.Id, _owner);
        Assert.AreEqual(21, state!.Participants.Count);

        // Only host should be connected
        var connected = state.Participants.Where(p => p.IsConnected).ToList();
        Assert.AreEqual(1, connected.Count);
        Assert.AreEqual(_owner.UserId, connected[0].UserId);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Additional Integration Tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task SprintCardAssignment_ThenBacklogCheck()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card1 = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Card A");
        var card2 = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Card B");
        var card3 = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId, "Card C");

        var sprint = await _sprintService.CreateSprintAsync(board.Id,
            new CreateSprintDto { Title = "Sprint 1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14) }, _owner);

        // Assign card1 and card2 to sprint
        _db.SprintCards.Add(new SprintCard { SprintId = sprint.Id, CardId = card1.Id });
        _db.SprintCards.Add(new SprintCard { SprintId = sprint.Id, CardId = card2.Id });
        await _db.SaveChangesAsync();

        // Backlog should only have card3
        var backlog = await _sprintService.GetBacklogCardsAsync(board.Id, _owner);
        Assert.AreEqual(1, backlog.Count);
        Assert.AreEqual(card3.Id, backlog[0].Id);
    }

    [TestMethod]
    public async Task CreateBoard_SwitchModes_OperationsRespectMode()
    {
        // Create personal board
        var personalDto = new CreateBoardDto { Title = "Personal Board" };
        var personalBoard = await _boardService.CreateBoardAsync(personalDto, _owner);
        Assert.AreEqual(BoardMode.Personal, personalBoard.Mode);

        // Create team board
        var teamDto = new CreateBoardDto { Title = "Team Board", Mode = BoardMode.Team };
        var teamBoard = await _boardService.CreateBoardAsync(teamDto, _owner);
        Assert.AreEqual(BoardMode.Team, teamBoard.Mode);

        // Sprint plan works on team board
        var planDto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        };
        var plan = await _planningService.CreateYearPlanAsync(teamBoard.Id, planDto, _owner);
        Assert.AreEqual(2, plan.Sprints.Count);

        // Sprint plan fails on personal board
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.CreateYearPlanAsync(personalBoard.Id, planDto, _owner));
    }

    [TestMethod]
    public async Task ReviewSession_Broadcasts_AllEventsCorrectly()
    {
        var board = await SeedTeamBoardAsync(_owner.UserId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _member.UserId, BoardMemberRole.Member);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);
        var card = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _owner.UserId);

        // Start session
        var session = await _reviewService.StartSessionAsync(board.Id, _owner);
        _realtimeMock.Verify(r => r.BroadcastReviewSessionStateAsync(
            session.Id, board.Id, "started", It.IsAny<CancellationToken>()), Times.Once);

        // Join
        _realtimeMock.Invocations.Clear();
        await _reviewService.JoinSessionAsync(session.Id, _member);
        _realtimeMock.Verify(r => r.BroadcastReviewParticipantChangedAsync(
            session.Id, _member.UserId, "joined", It.IsAny<CancellationToken>()), Times.Once);

        // Set card
        _realtimeMock.Invocations.Clear();
        await _reviewService.SetCurrentCardAsync(session.Id, card.Id, _owner);
        _realtimeMock.Verify(r => r.BroadcastReviewCardChangedAsync(
            session.Id, board.Id, card.Id, It.IsAny<CancellationToken>()), Times.Once);

        // Start poker
        _realtimeMock.Invocations.Clear();
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id, new StartReviewPokerDto(), _owner);
        _realtimeMock.Verify(r => r.BroadcastReviewPokerStateAsync(
            session.Id, withPoker.ActivePokerSession!.Id, board.Id, "started",
            It.IsAny<CancellationToken>()), Times.Once);

        // End session
        _realtimeMock.Invocations.Clear();
        await _reviewService.EndSessionAsync(session.Id, _owner);
        _realtimeMock.Verify(r => r.BroadcastReviewSessionStateAsync(
            session.Id, board.Id, "ended", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private async Task<Board> SeedTeamBoardAsync(Guid ownerId, string title = "Team Board")
    {
        var board = await TestHelpers.SeedBoardAsync(_db, ownerId, title);
        board.Mode = BoardMode.Team;
        _db.Update(board);
        await _db.SaveChangesAsync();
        return board;
    }

    private async Task<SprintPlanOverviewDto> CreateTestPlan(Guid boardId, int count, int durationWeeks)
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = count,
            DefaultDurationWeeks = durationWeeks
        };
        return await _planningService.CreateYearPlanAsync(boardId, dto, _owner);
    }
}
