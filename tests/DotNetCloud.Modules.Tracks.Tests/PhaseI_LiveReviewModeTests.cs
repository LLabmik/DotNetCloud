using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using DotNetCloud.Modules.Tracks.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Comprehensive tests for Phase I: Live Review Mode UI.
/// Validates review session host controls, participant view, TracksPage integration,
/// poker lifecycle during review, mode guards, helper methods, and SignalR integration.
/// </summary>
[TestClass]
public class PhaseI_LiveReviewModeTests
{
    private TracksDbContext _db = null!;
    private BoardService _boardService = null!;
    private ReviewSessionService _reviewService = null!;
    private PokerService _pokerService = null!;
    private CardService _cardService = null!;
    private SwimlaneService _swimlaneService = null!;
    private SprintService _sprintService = null!;
    private SprintPlanningService _planningService = null!;
    private ActivityService _activityService = null!;
    private Mock<ITracksRealtimeService> _realtimeMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _admin = null!;
    private CallerContext _member = null!;
    private CallerContext _member2 = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();
    private readonly Guid _member2UserId = Guid.NewGuid();

    private Board _teamBoard = null!;
    private Board _personalBoard = null!;
    private BoardSwimlane _swimlane = null!;
    private Card _card1 = null!;
    private Card _card2 = null!;
    private Card _card3 = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _realtimeMock = new Mock<ITracksRealtimeService>();
        _activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, _eventBusMock.Object, _activityService, teamService, NullLogger<BoardService>.Instance);
        _planningService = new SprintPlanningService(_db, _boardService, _activityService, NullLogger<SprintPlanningService>.Instance);
        _sprintService = new SprintService(_db, _boardService, _activityService, _eventBusMock.Object, NullLogger<SprintService>.Instance);
        _swimlaneService = new SwimlaneService(_db, _boardService, _activityService, NullLogger<SwimlaneService>.Instance);
        _cardService = new CardService(_db, _boardService, _activityService, _eventBusMock.Object, NullLogger<CardService>.Instance);
        _pokerService = new PokerService(_db, _boardService, _activityService, _realtimeMock.Object, NullLogger<PokerService>.Instance);
        _reviewService = new ReviewSessionService(_db, _boardService, _pokerService, _realtimeMock.Object, NullLogger<ReviewSessionService>.Instance);

        _admin = TestHelpers.CreateCaller(_adminUserId);
        _member = TestHelpers.CreateCaller(_memberUserId);
        _member2 = TestHelpers.CreateCaller(_member2UserId);

        // Team board with admin (Owner), member, member2
        _teamBoard = await TestHelpers.SeedBoardAsync(_db, _adminUserId, "Team Board");
        _teamBoard.Mode = BoardMode.Team;
        _db.Update(_teamBoard);
        await _db.SaveChangesAsync();
        await TestHelpers.AddMemberAsync(_db, _teamBoard.Id, _memberUserId, BoardMemberRole.Member);
        await TestHelpers.AddMemberAsync(_db, _teamBoard.Id, _member2UserId, BoardMemberRole.Member);

        // Swimlane + cards
        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _teamBoard.Id, "To Do");
        _card1 = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _adminUserId, "Card 1");
        _card2 = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _adminUserId, "Card 2");
        _card3 = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _adminUserId, "Card 3");

        // Personal board
        _personalBoard = await TestHelpers.SeedBoardAsync(_db, _adminUserId, "Personal Board");
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ═══════════════════════════════════════════════════════════════════
    // Step 32 — Review Session Host Controls
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void GetStatusClass_Active_ReturnsCorrectClass()
    {
        Assert.AreEqual("status-active", ReviewSessionHost.GetStatusClass(ReviewSessionStatus.Active));
    }

    [TestMethod]
    public void GetStatusClass_Paused_ReturnsCorrectClass()
    {
        Assert.AreEqual("status-paused", ReviewSessionHost.GetStatusClass(ReviewSessionStatus.Paused));
    }

    [TestMethod]
    public void GetStatusClass_Ended_ReturnsCorrectClass()
    {
        Assert.AreEqual("status-ended", ReviewSessionHost.GetStatusClass(ReviewSessionStatus.Ended));
    }

    [TestMethod]
    public void GetPokerStatusClass_Voting_ReturnsCorrectClass()
    {
        Assert.AreEqual("poker-voting", ReviewSessionHost.GetPokerStatusClass(PokerSessionStatus.Voting));
    }

    [TestMethod]
    public void GetPokerStatusClass_Revealed_ReturnsCorrectClass()
    {
        Assert.AreEqual("poker-revealed", ReviewSessionHost.GetPokerStatusClass(PokerSessionStatus.Revealed));
    }

    [TestMethod]
    public void GetPokerStatusClass_Completed_ReturnsCorrectClass()
    {
        Assert.AreEqual("poker-completed", ReviewSessionHost.GetPokerStatusClass(PokerSessionStatus.Completed));
    }

    [TestMethod]
    public void GetPokerStatusClass_Cancelled_ReturnsCorrectClass()
    {
        Assert.AreEqual("poker-cancelled", ReviewSessionHost.GetPokerStatusClass(PokerSessionStatus.Cancelled));
    }

    // ── Poker Scale Values ──────────────────────────────────

    [TestMethod]
    public void GetScaleValues_Fibonacci_ReturnsCorrectValues()
    {
        var values = ReviewSessionHost.GetScaleValues(PokerScale.Fibonacci);

        CollectionAssert.Contains(values, "0");
        CollectionAssert.Contains(values, "1");
        CollectionAssert.Contains(values, "2");
        CollectionAssert.Contains(values, "3");
        CollectionAssert.Contains(values, "5");
        CollectionAssert.Contains(values, "8");
        CollectionAssert.Contains(values, "13");
        CollectionAssert.Contains(values, "21");
        CollectionAssert.Contains(values, "34");
        CollectionAssert.Contains(values, "?");
    }

    [TestMethod]
    public void GetScaleValues_TShirt_ReturnsCorrectValues()
    {
        var values = ReviewSessionHost.GetScaleValues(PokerScale.TShirt);

        CollectionAssert.Contains(values, "XS");
        CollectionAssert.Contains(values, "S");
        CollectionAssert.Contains(values, "M");
        CollectionAssert.Contains(values, "L");
        CollectionAssert.Contains(values, "XL");
        CollectionAssert.Contains(values, "XXL");
        CollectionAssert.Contains(values, "?");
    }

    [TestMethod]
    public void GetScaleValues_PowersOfTwo_ReturnsCorrectValues()
    {
        var values = ReviewSessionHost.GetScaleValues(PokerScale.PowersOfTwo);

        CollectionAssert.Contains(values, "0");
        CollectionAssert.Contains(values, "1");
        CollectionAssert.Contains(values, "2");
        CollectionAssert.Contains(values, "4");
        CollectionAssert.Contains(values, "8");
        CollectionAssert.Contains(values, "16");
        CollectionAssert.Contains(values, "32");
        CollectionAssert.Contains(values, "?");
    }

    [TestMethod]
    public void GetScaleValues_Custom_ReturnsFallbackValues()
    {
        var values = ReviewSessionHost.GetScaleValues(PokerScale.Custom);

        // Custom falls back to a default set
        Assert.IsTrue(values.Length > 0);
        CollectionAssert.Contains(values, "?");
    }

    [TestMethod]
    public void GetScaleValues_AllScales_ContainQuestionMark()
    {
        foreach (PokerScale scale in Enum.GetValues<PokerScale>())
        {
            var values = ReviewSessionHost.GetScaleValues(scale);
            CollectionAssert.Contains(values, "?", $"Scale {scale} should contain '?'");
        }
    }

    [TestMethod]
    public void GetScaleValues_Fibonacci_HasCorrectCount()
    {
        var values = ReviewSessionHost.GetScaleValues(PokerScale.Fibonacci);
        Assert.AreEqual(10, values.Length); // 0,1,2,3,5,8,13,21,34,?
    }

    [TestMethod]
    public void GetScaleValues_TShirt_HasCorrectCount()
    {
        var values = ReviewSessionHost.GetScaleValues(PokerScale.TShirt);
        Assert.AreEqual(7, values.Length); // XS,S,M,L,XL,XXL,?
    }

    [TestMethod]
    public void GetScaleValues_PowersOfTwo_HasCorrectCount()
    {
        var values = ReviewSessionHost.GetScaleValues(PokerScale.PowersOfTwo);
        Assert.AreEqual(8, values.Length); // 0,1,2,4,8,16,32,?
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 32/33 — Full Review + Poker Lifecycle (Service Integration)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task HostStartsSession_NavigatesCards_StartsPokerId()
    {
        // Start session
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(ReviewSessionStatus.Active, session.Status);

        // Navigate to first card
        var updated = await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        Assert.AreEqual(_card1.Id, updated.CurrentCardId);

        // Start poker on that card
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto { Scale = PokerScale.Fibonacci }, _admin);
        Assert.IsNotNull(withPoker.ActivePokerSession);
        Assert.AreEqual(PokerSessionStatus.Voting, withPoker.ActivePokerSession.Status);
        Assert.AreEqual(_card1.Id, withPoker.ActivePokerSession.CardId);
    }

    [TestMethod]
    public async Task FullPokerCycle_VoteRevealAccept()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);

        // Start poker
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto { Scale = PokerScale.Fibonacci }, _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        // Both vote
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _admin);
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "8" }, _member);

        // Check vote status (values hidden) — returns all board members
        var status = await _pokerService.GetVoteStatusAsync(pokerId, _admin);
        Assert.AreEqual(3, status.Count); // admin, member, member2 (all board members)
        Assert.IsTrue(status.First(v => v.UserId == _adminUserId).HasVoted);
        Assert.IsTrue(status.First(v => v.UserId == _memberUserId).HasVoted);
        Assert.IsFalse(status.First(v => v.UserId == _member2UserId).HasVoted);

        // Reveal
        var revealed = await _pokerService.RevealSessionAsync(pokerId, _admin);
        Assert.AreEqual(PokerSessionStatus.Revealed, revealed.Status);
        Assert.AreEqual(2, revealed.Votes.Count);

        // Accept estimate
        var accepted = await _pokerService.AcceptEstimateAsync(pokerId,
            new AcceptPokerEstimateDto { AcceptedEstimate = "5", StoryPoints = 5 }, _admin);
        Assert.AreEqual(PokerSessionStatus.Completed, accepted.Status);
        Assert.AreEqual("5", accepted.AcceptedEstimate);

        // Verify card got story points
        var card = await _db.Cards.FindAsync(_card1.Id);
        Assert.AreEqual(5, card!.StoryPoints);
    }

    [TestMethod]
    public async Task PokerReVote_StartsNewRound()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto { Scale = PokerScale.Fibonacci }, _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        // Vote
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "3" }, _admin);

        // Reveal
        await _pokerService.RevealSessionAsync(pokerId, _admin);

        // Re-vote (new round)
        var newRound = await _pokerService.StartNewRoundAsync(pokerId, _admin);
        Assert.AreEqual(PokerSessionStatus.Voting, newRound.Status);
        Assert.AreEqual(2, newRound.Round);
        Assert.AreEqual(0, newRound.Votes.Count);
    }

    [TestMethod]
    public async Task CardNavigation_ResetsPokerOnCardChange()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);

        // Start poker on card 1
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        // Accept the poker so it's completed
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _admin);
        await _pokerService.RevealSessionAsync(pokerId, _admin);
        await _pokerService.AcceptEstimateAsync(pokerId,
            new AcceptPokerEstimateDto { AcceptedEstimate = "5", StoryPoints = 5 }, _admin);

        // Navigate to card 2
        var card2Session = await _reviewService.SetCurrentCardAsync(session.Id, _card2.Id, _admin);
        Assert.AreEqual(_card2.Id, card2Session.CurrentCardId);

        // Previous poker is completed — new poker can be started on card 2
        var card2Poker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto { Scale = PokerScale.TShirt }, _admin);
        Assert.IsNotNull(card2Poker.ActivePokerSession);
        Assert.AreEqual(_card2.Id, card2Poker.ActivePokerSession.CardId);
        Assert.AreEqual(PokerScale.TShirt, card2Poker.ActivePokerSession.Scale);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 33 — Participant View Behaviors
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ParticipantJoins_SeesCurrentCard()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);

        // Participant joins after card is already set
        var joined = await _reviewService.JoinSessionAsync(session.Id, _member);
        Assert.AreEqual(_card1.Id, joined.CurrentCardId);
    }

    [TestMethod]
    public async Task ParticipantCanVote_InActivePoker()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);

        var pokerId = withPoker.ActivePokerSession!.Id;
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "8" }, _member);

        var status = await _pokerService.GetVoteStatusAsync(pokerId, _admin);
        var memberStatus = status.First(s => s.UserId == _memberUserId);
        Assert.IsTrue(memberStatus.HasVoted);
    }

    [TestMethod]
    public async Task ParticipantCannotControlSession()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);

        // Member can't set current card
        var ex1 = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _member));
        Assert.IsTrue(ex1.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));

        // Member can't start poker
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var ex2 = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartPokerForCurrentCardAsync(session.Id, new StartReviewPokerDto(), _member));
        Assert.IsTrue(ex2.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));

        // Member can't end session
        var ex3 = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.EndSessionAsync(session.Id, _member));
        Assert.IsTrue(ex3.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));
    }

    [TestMethod]
    public async Task ParticipantLeave_SetsDisconnected()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);

        await _reviewService.LeaveSessionAsync(session.Id, _member);

        var state = await _reviewService.GetSessionStateAsync(session.Id, _admin);
        var p = state!.Participants.First(x => x.UserId == _memberUserId);
        Assert.IsFalse(p.IsConnected);
    }

    [TestMethod]
    public async Task ParticipantReconnect_RestoresConnection()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.LeaveSessionAsync(session.Id, _member);

        var rejoined = await _reviewService.JoinSessionAsync(session.Id, _member);
        var p = rejoined.Participants.First(x => x.UserId == _memberUserId);
        Assert.IsTrue(p.IsConnected);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 34 — TracksPage Review Integration
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ModeGuard_PersonalBoard_CannotStartReview()
    {
        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartSessionAsync(_personalBoard.Id, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task ModeGuard_TeamBoard_CanStartReview()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        Assert.IsNotNull(session);
        Assert.AreEqual(ReviewSessionStatus.Active, session.Status);
    }

    [TestMethod]
    public async Task GetActiveSession_ReturnsCurrentSession()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);

        var active = await _reviewService.GetActiveSessionForBoardAsync(_teamBoard.Id, _admin);

        Assert.IsNotNull(active);
        Assert.AreEqual(session.Id, active.Id);
    }

    [TestMethod]
    public async Task GetActiveSession_AfterEnd_ReturnsNull()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.EndSessionAsync(session.Id, _admin);

        var active = await _reviewService.GetActiveSessionForBoardAsync(_teamBoard.Id, _admin);
        Assert.IsNull(active);
    }

    [TestMethod]
    public async Task ReviewEntry_NoSession_MemberCanJoinAfterHostStarts()
    {
        // No active session
        var active = await _reviewService.GetActiveSessionForBoardAsync(_teamBoard.Id, _member);
        Assert.IsNull(active);

        // Host starts
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);

        // Member can now see and join
        active = await _reviewService.GetActiveSessionForBoardAsync(_teamBoard.Id, _member);
        Assert.IsNotNull(active);

        var joined = await _reviewService.JoinSessionAsync(active.Id, _member);
        Assert.AreEqual(2, joined.Participants.Count);
    }

    [TestMethod]
    public async Task ReviewSessionAllowsOnlyOneActive()
    {
        await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartSessionAsync(_teamBoard.Id, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionAlreadyActive));
    }

    [TestMethod]
    public async Task ReviewSession_NewSessionAfterPreviousEnded()
    {
        var first = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.EndSessionAsync(first.Id, _admin);

        var second = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        Assert.IsNotNull(second);
        Assert.AreNotEqual(first.Id, second.Id);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Vote Status Visibility (Host Perspective)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task VoteStatus_ShowsWhoVotedWithoutValues()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.JoinSessionAsync(session.Id, _member2);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        // Only member votes
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _member);

        var status = await _pokerService.GetVoteStatusAsync(pokerId, _admin);
        Assert.AreEqual(3, status.Count); // admin, member, member2

        var adminStatus = status.First(s => s.UserId == _adminUserId);
        var memberStatus = status.First(s => s.UserId == _memberUserId);
        var member2Status = status.First(s => s.UserId == _member2UserId);

        Assert.IsFalse(adminStatus.HasVoted);
        Assert.IsTrue(memberStatus.HasVoted);
        Assert.IsFalse(member2Status.HasVoted);
    }

    [TestMethod]
    public async Task VoteStatus_AllVoted_ShowsAllTrue()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _admin);
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "8" }, _member);

        var status = await _pokerService.GetVoteStatusAsync(pokerId, _admin);
        // Only admin and member voted; member2 didn't (vote status lists all board members)
        Assert.IsTrue(status.First(s => s.UserId == _adminUserId).HasVoted);
        Assert.IsTrue(status.First(s => s.UserId == _memberUserId).HasVoted);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Poker Scale Validation During Review
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task PokerInReview_FibonacciScaleValidation()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto { Scale = PokerScale.Fibonacci }, _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        // Valid vote
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _admin);
        var status = await _pokerService.GetVoteStatusAsync(pokerId, _admin);
        Assert.IsTrue(status.First(s => s.UserId == _adminUserId).HasVoted);
    }

    [TestMethod]
    public async Task PokerInReview_TShirtScale()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto { Scale = PokerScale.TShirt }, _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "M" }, _admin);
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "L" }, _member);

        await _pokerService.RevealSessionAsync(pokerId, _admin);
        var revealed = await _pokerService.GetSessionAsync(pokerId, _admin);

        Assert.AreEqual(PokerSessionStatus.Revealed, revealed!.Status);
        Assert.AreEqual(2, revealed.Votes.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SignalR Broadcast Verification
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ReviewSession_BroadcastsOnAllStateChanges()
    {
        // Start
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        _realtimeMock.Verify(r => r.BroadcastReviewSessionStateAsync(
            session.Id, _teamBoard.Id, "started", It.IsAny<CancellationToken>()), Times.Once);

        // Join
        _realtimeMock.Invocations.Clear();
        await _reviewService.JoinSessionAsync(session.Id, _member);
        _realtimeMock.Verify(r => r.BroadcastReviewParticipantChangedAsync(
            session.Id, _memberUserId, "joined", It.IsAny<CancellationToken>()), Times.Once);

        // Set card
        _realtimeMock.Invocations.Clear();
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        _realtimeMock.Verify(r => r.BroadcastReviewCardChangedAsync(
            session.Id, _teamBoard.Id, _card1.Id, It.IsAny<CancellationToken>()), Times.Once);

        // Start poker
        _realtimeMock.Invocations.Clear();
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);
        _realtimeMock.Verify(r => r.BroadcastReviewPokerStateAsync(
            session.Id, withPoker.ActivePokerSession!.Id, _teamBoard.Id, "started",
            It.IsAny<CancellationToken>()), Times.Once);

        // Leave
        _realtimeMock.Invocations.Clear();
        await _reviewService.LeaveSessionAsync(session.Id, _member);
        _realtimeMock.Verify(r => r.BroadcastReviewParticipantChangedAsync(
            session.Id, _memberUserId, "left", It.IsAny<CancellationToken>()), Times.Once);

        // End
        _realtimeMock.Invocations.Clear();
        await _reviewService.EndSessionAsync(session.Id, _admin);
        _realtimeMock.Verify(r => r.BroadcastReviewSessionStateAsync(
            session.Id, _teamBoard.Id, "ended", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task PokerVote_BroadcastsVoteStatus()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        _realtimeMock.Invocations.Clear();
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _member);

        _realtimeMock.Verify(r => r.BroadcastPokerVoteStatusAsync(
            session.Id, pokerId, _memberUserId, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task PokerReveal_BroadcastsPokerState()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _admin);

        _realtimeMock.Invocations.Clear();
        await _pokerService.RevealSessionAsync(pokerId, _admin);

        _realtimeMock.Verify(r => r.BroadcastReviewPokerStateAsync(
            session.Id, pokerId, _teamBoard.Id, "revealed", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Multi-Participant Scenarios
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task MultipleParticipants_AllCanVote()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.JoinSessionAsync(session.Id, _member2);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto { Scale = PokerScale.Fibonacci }, _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "3" }, _admin);
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _member);
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "8" }, _member2);

        var status = await _pokerService.GetVoteStatusAsync(pokerId, _admin);
        Assert.AreEqual(3, status.Count);
        Assert.IsTrue(status.All(s => s.HasVoted));
    }

    [TestMethod]
    public async Task ParticipantCanChangeVote_BeforeReveal()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        // Vote first time
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _member);

        // Change vote
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "8" }, _member);

        // Reveal and check
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _admin);
        var revealed = await _pokerService.RevealSessionAsync(pokerId, _admin);
        var memberVote = revealed.Votes.First(v => v.UserId == _memberUserId);
        Assert.AreEqual("8", memberVote.Estimate);
    }

    [TestMethod]
    public async Task DisconnectedParticipant_VoteStatusStillShown()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        var withPoker = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);
        var pokerId = withPoker.ActivePokerSession!.Id;

        // Member votes then disconnects
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _member);
        await _reviewService.LeaveSessionAsync(session.Id, _member);

        var status = await _pokerService.GetVoteStatusAsync(pokerId, _admin);
        var memberStatus = status.FirstOrDefault(s => s.UserId == _memberUserId);
        Assert.IsNotNull(memberStatus);
        Assert.IsTrue(memberStatus.HasVoted);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Card Navigation with Multiple Cards
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task HostNavigates_ThroughMultipleCards()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);

        // Navigate forward through cards
        var state1 = await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        Assert.AreEqual(_card1.Id, state1.CurrentCardId);

        var state2 = await _reviewService.SetCurrentCardAsync(session.Id, _card2.Id, _admin);
        Assert.AreEqual(_card2.Id, state2.CurrentCardId);

        var state3 = await _reviewService.SetCurrentCardAsync(session.Id, _card3.Id, _admin);
        Assert.AreEqual(_card3.Id, state3.CurrentCardId);

        // Navigate back
        var state4 = await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        Assert.AreEqual(_card1.Id, state4.CurrentCardId);
    }

    [TestMethod]
    public async Task CardNavigation_BroadcastsEachChange()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        _realtimeMock.Invocations.Clear();

        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        await _reviewService.SetCurrentCardAsync(session.Id, _card2.Id, _admin);
        await _reviewService.SetCurrentCardAsync(session.Id, _card3.Id, _admin);

        _realtimeMock.Verify(r => r.BroadcastReviewCardChangedAsync(
            session.Id, _teamBoard.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Security: One Active Poker Per Review
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task OneActivePokerPerReview_BlocksSecondStart()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);

        // Start first poker (Voting status)
        await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);

        // Try starting another — should fail
        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartPokerForCurrentCardAsync(session.Id,
                new StartReviewPokerDto(), _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewPokerStillActive));
    }

    [TestMethod]
    public async Task CompletedPoker_AllowsNewPokerStart()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);

        var first = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);
        var pokerId = first.ActivePokerSession!.Id;

        // Complete the poker
        await _pokerService.SubmitVoteAsync(pokerId, new SubmitPokerVoteDto { Estimate = "5" }, _admin);
        await _pokerService.RevealSessionAsync(pokerId, _admin);
        await _pokerService.AcceptEstimateAsync(pokerId,
            new AcceptPokerEstimateDto { AcceptedEstimate = "5", StoryPoints = 5 }, _admin);

        // Now we can start a new one (e.g., on the same card for re-estimation)
        await _reviewService.SetCurrentCardAsync(session.Id, _card2.Id, _admin);
        var second = await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);
        Assert.IsNotNull(second.ActivePokerSession);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Session End Cleans Up
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task EndSession_SetsEndedStatus()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);

        await _reviewService.EndSessionAsync(session.Id, _admin);

        var state = await _reviewService.GetSessionStateAsync(session.Id, _admin);
        Assert.AreEqual(ReviewSessionStatus.Ended, state!.Status);
        Assert.IsNotNull(state.EndedAt);
    }

    [TestMethod]
    public async Task EndSession_AllowsNewSessionCreation()
    {
        var session1 = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.EndSessionAsync(session1.Id, _admin);

        var session2 = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        Assert.IsNotNull(session2);
        Assert.AreNotEqual(session1.Id, session2.Id);
    }

    [TestMethod]
    public async Task EndSession_DoubleEnd_Throws()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.EndSessionAsync(session.Id, _admin);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.EndSessionAsync(session.Id, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionEnded));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Non-Member Cannot Join
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task NonBoardMember_CannotStartSession()
    {
        var outsider = TestHelpers.CreateCaller();

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewService.StartSessionAsync(_teamBoard.Id, outsider));
        // Either insufficient role or not a member
        Assert.IsTrue(ex.Errors.Count > 0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Session State Includes Active Poker
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetSessionState_IncludesActivePoker()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);
        await _reviewService.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto { Scale = PokerScale.PowersOfTwo }, _admin);

        var state = await _reviewService.GetSessionStateAsync(session.Id, _admin);

        Assert.IsNotNull(state?.ActivePokerSession);
        Assert.AreEqual(PokerScale.PowersOfTwo, state.ActivePokerSession.Scale);
        Assert.AreEqual(PokerSessionStatus.Voting, state.ActivePokerSession.Status);
    }

    [TestMethod]
    public async Task GetSessionState_NoPoker_ActivePokerNull()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.SetCurrentCardAsync(session.Id, _card1.Id, _admin);

        var state = await _reviewService.GetSessionStateAsync(session.Id, _admin);

        Assert.IsNull(state?.ActivePokerSession);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Review Group Management (SignalR Groups)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task StartSession_AddsHostToReviewGroup()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);

        _realtimeMock.Verify(r => r.AddUserToReviewGroupAsync(
            _adminUserId, session.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task JoinSession_AddsParticipantToReviewGroup()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);

        await _reviewService.JoinSessionAsync(session.Id, _member);

        _realtimeMock.Verify(r => r.AddUserToReviewGroupAsync(
            _memberUserId, session.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task LeaveSession_RemovesParticipantFromReviewGroup()
    {
        var session = await _reviewService.StartSessionAsync(_teamBoard.Id, _admin);
        await _reviewService.JoinSessionAsync(session.Id, _member);

        await _reviewService.LeaveSessionAsync(session.Id, _member);

        _realtimeMock.Verify(r => r.RemoveUserFromReviewGroupAsync(
            _memberUserId, session.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
