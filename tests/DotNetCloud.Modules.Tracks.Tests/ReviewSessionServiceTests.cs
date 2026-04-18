using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Tests for <see cref="ReviewSessionService"/> covering the full review session lifecycle,
/// security/authorization, and real-time broadcast integration (Phase A + B + D).
/// </summary>
[TestClass]
public class ReviewSessionServiceTests
{
    private TracksDbContext _db = null!;
    private ReviewSessionService _service = null!;
    private BoardService _boardService = null!;
    private PokerService _pokerService = null!;
    private Mock<ITracksRealtimeService> _realtimeMock = null!;
    private CallerContext _admin = null!;
    private CallerContext _member = null!;
    private Board _board = null!;
    private BoardSwimlane _swimlane = null!;
    private Card _card = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _admin = TestHelpers.CreateCaller();
        _member = TestHelpers.CreateCaller();
        _realtimeMock = new Mock<ITracksRealtimeService>();

        var eventBus = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, eventBus.Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, eventBus.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _pokerService = new PokerService(_db, _boardService, activityService, _realtimeMock.Object, NullLogger<PokerService>.Instance);
        _service = new ReviewSessionService(_db, _boardService, _pokerService, _realtimeMock.Object, NullLogger<ReviewSessionService>.Instance);

        // Seed a Team-mode board with admin (Owner) + member
        _board = await TestHelpers.SeedBoardAsync(_db, _admin.UserId);
        _board.Mode = BoardMode.Team;
        _db.Update(_board);
        await _db.SaveChangesAsync();

        await TestHelpers.AddMemberAsync(_db, _board.Id, _member.UserId, BoardMemberRole.Member);
        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        _card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _admin.UserId);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── StartSession ─────────────────────────────────────────────────

    [TestMethod]
    public async Task StartSession_TeamBoard_CreatesSession()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);

        Assert.IsNotNull(session);
        Assert.AreEqual(_board.Id, session.BoardId);
        Assert.AreEqual(_admin.UserId, session.HostUserId);
        Assert.AreEqual(ReviewSessionStatus.Active, session.Status);
        Assert.IsNull(session.CurrentCardId);
        Assert.IsNull(session.EndedAt);
        Assert.AreEqual(1, session.Participants.Count);
        Assert.AreEqual(_admin.UserId, session.Participants[0].UserId);
        Assert.IsTrue(session.Participants[0].IsConnected);
    }

    [TestMethod]
    public async Task StartSession_PersonalBoard_Throws()
    {
        _board.Mode = BoardMode.Personal;
        _db.Update(_board);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.StartSessionAsync(_board.Id, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task StartSession_NonAdmin_Throws()
    {
        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.StartSessionAsync(_board.Id, _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InsufficientBoardRole));
    }

    [TestMethod]
    public async Task StartSession_ActiveSessionExists_Throws()
    {
        await _service.StartSessionAsync(_board.Id, _admin);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.StartSessionAsync(_board.Id, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionAlreadyActive));
    }

    [TestMethod]
    public async Task StartSession_BroadcastsSessionStarted()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);

        _realtimeMock.Verify(r => r.AddUserToReviewGroupAsync(
            _admin.UserId, session.Id, It.IsAny<CancellationToken>()), Times.Once);
        _realtimeMock.Verify(r => r.BroadcastReviewSessionStateAsync(
            session.Id, _board.Id, "started", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── JoinSession ──────────────────────────────────────────────────

    [TestMethod]
    public async Task JoinSession_MemberJoins_AddsParticipant()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);

        var updated = await _service.JoinSessionAsync(session.Id, _member);

        Assert.AreEqual(2, updated.Participants.Count);
        Assert.IsTrue(updated.Participants.Any(p => p.UserId == _member.UserId && p.IsConnected));
    }

    [TestMethod]
    public async Task JoinSession_Reconnect_IsConnectedTrue()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.JoinSessionAsync(session.Id, _member);
        await _service.LeaveSessionAsync(session.Id, _member);

        // Reconnect
        var updated = await _service.JoinSessionAsync(session.Id, _member);

        var participant = updated.Participants.First(p => p.UserId == _member.UserId);
        Assert.IsTrue(participant.IsConnected);
    }

    [TestMethod]
    public async Task JoinSession_SessionNotFound_Throws()
    {
        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.JoinSessionAsync(Guid.NewGuid(), _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotFound));
    }

    [TestMethod]
    public async Task JoinSession_BroadcastsParticipantJoined()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        _realtimeMock.Invocations.Clear();

        await _service.JoinSessionAsync(session.Id, _member);

        _realtimeMock.Verify(r => r.AddUserToReviewGroupAsync(
            _member.UserId, session.Id, It.IsAny<CancellationToken>()), Times.Once);
        _realtimeMock.Verify(r => r.BroadcastReviewParticipantChangedAsync(
            session.Id, _member.UserId, "joined", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── LeaveSession ─────────────────────────────────────────────────

    [TestMethod]
    public async Task LeaveSession_SetsDisconnected()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.JoinSessionAsync(session.Id, _member);

        await _service.LeaveSessionAsync(session.Id, _member);

        var state = await _service.GetSessionStateAsync(session.Id, _admin);
        var participant = state!.Participants.First(p => p.UserId == _member.UserId);
        Assert.IsFalse(participant.IsConnected);
    }

    [TestMethod]
    public async Task LeaveSession_NonParticipant_NoOp()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);

        // Should not throw — graceful no-op
        await _service.LeaveSessionAsync(session.Id, _member);
    }

    [TestMethod]
    public async Task LeaveSession_BroadcastsParticipantLeft()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.JoinSessionAsync(session.Id, _member);
        _realtimeMock.Invocations.Clear();

        await _service.LeaveSessionAsync(session.Id, _member);

        _realtimeMock.Verify(r => r.RemoveUserFromReviewGroupAsync(
            _member.UserId, session.Id, It.IsAny<CancellationToken>()), Times.Once);
        _realtimeMock.Verify(r => r.BroadcastReviewParticipantChangedAsync(
            session.Id, _member.UserId, "left", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── SetCurrentCard ───────────────────────────────────────────────

    [TestMethod]
    public async Task SetCurrentCard_ValidCard_Updates()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);

        var updated = await _service.SetCurrentCardAsync(session.Id, _card.Id, _admin);

        Assert.AreEqual(_card.Id, updated.CurrentCardId);
    }

    [TestMethod]
    public async Task SetCurrentCard_NonHost_Throws()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.JoinSessionAsync(session.Id, _member);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.SetCurrentCardAsync(session.Id, _card.Id, _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));
    }

    [TestMethod]
    public async Task SetCurrentCard_CardFromOtherBoard_Throws()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);

        // Create another board with a card
        var otherBoard = await TestHelpers.SeedBoardAsync(_db, _admin.UserId, "Other Board");
        var otherSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, otherBoard.Id);
        var otherCard = await TestHelpers.SeedCardAsync(_db, otherSwimlane.Id, _admin.UserId);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.SetCurrentCardAsync(session.Id, otherCard.Id, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.CardNotFound));
    }

    [TestMethod]
    public async Task SetCurrentCard_BroadcastsCardChanged()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        _realtimeMock.Invocations.Clear();

        await _service.SetCurrentCardAsync(session.Id, _card.Id, _admin);

        _realtimeMock.Verify(r => r.BroadcastReviewCardChangedAsync(
            session.Id, _board.Id, _card.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── StartPokerForCurrentCard ─────────────────────────────────────

    [TestMethod]
    public async Task StartPoker_ValidCard_CreatesLinkedPoker()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.SetCurrentCardAsync(session.Id, _card.Id, _admin);

        var updated = await _service.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);

        Assert.IsNotNull(updated.ActivePokerSession);
        Assert.AreEqual(_card.Id, updated.ActivePokerSession.CardId);
    }

    [TestMethod]
    public async Task StartPoker_NoCurrentCard_Throws()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.StartPokerForCurrentCardAsync(session.Id,
                new StartReviewPokerDto(), _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.CardNotFound));
    }

    [TestMethod]
    public async Task StartPoker_NonHost_Throws()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.SetCurrentCardAsync(session.Id, _card.Id, _admin);
        await _service.JoinSessionAsync(session.Id, _member);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.StartPokerForCurrentCardAsync(session.Id,
                new StartReviewPokerDto(), _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));
    }

    [TestMethod]
    public async Task StartPoker_ActivePokerExists_Throws()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.SetCurrentCardAsync(session.Id, _card.Id, _admin);
        await _service.StartPokerForCurrentCardAsync(session.Id, new StartReviewPokerDto(), _admin);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.StartPokerForCurrentCardAsync(session.Id,
                new StartReviewPokerDto(), _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewPokerStillActive));
    }

    [TestMethod]
    public async Task StartPoker_BroadcastsPokerStarted()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.SetCurrentCardAsync(session.Id, _card.Id, _admin);
        _realtimeMock.Invocations.Clear();

        var updated = await _service.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);

        _realtimeMock.Verify(r => r.BroadcastReviewPokerStateAsync(
            session.Id, updated.ActivePokerSession!.Id, _board.Id, "started",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── EndSession ───────────────────────────────────────────────────

    [TestMethod]
    public async Task EndSession_Host_EndsSuccessfully()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);

        await _service.EndSessionAsync(session.Id, _admin);

        var state = await _service.GetSessionStateAsync(session.Id, _admin);
        Assert.AreEqual(ReviewSessionStatus.Ended, state!.Status);
        Assert.IsNotNull(state.EndedAt);
    }

    [TestMethod]
    public async Task EndSession_NonHost_Throws()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.JoinSessionAsync(session.Id, _member);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.EndSessionAsync(session.Id, _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));
    }

    [TestMethod]
    public async Task EndSession_AlreadyEnded_Throws()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.EndSessionAsync(session.Id, _admin);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.EndSessionAsync(session.Id, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionEnded));
    }

    [TestMethod]
    public async Task EndSession_BroadcastsSessionEnded()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        _realtimeMock.Invocations.Clear();

        await _service.EndSessionAsync(session.Id, _admin);

        _realtimeMock.Verify(r => r.BroadcastReviewSessionStateAsync(
            session.Id, _board.Id, "ended", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── GetSessionState + GetActiveSessionForBoard ───────────────────

    [TestMethod]
    public async Task GetSessionState_ReturnsFullState()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.JoinSessionAsync(session.Id, _member);
        await _service.SetCurrentCardAsync(session.Id, _card.Id, _admin);

        var state = await _service.GetSessionStateAsync(session.Id, _admin);

        Assert.IsNotNull(state);
        Assert.AreEqual(session.Id, state.Id);
        Assert.AreEqual(_card.Id, state.CurrentCardId);
        Assert.AreEqual(2, state.Participants.Count);
    }

    [TestMethod]
    public async Task GetSessionState_NonExistent_ReturnsNull()
    {
        var state = await _service.GetSessionStateAsync(Guid.NewGuid(), _admin);
        Assert.IsNull(state);
    }

    [TestMethod]
    public async Task GetActiveSessionForBoard_ReturnsActiveSession()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);

        var active = await _service.GetActiveSessionForBoardAsync(_board.Id, _admin);

        Assert.IsNotNull(active);
        Assert.AreEqual(session.Id, active.Id);
    }

    [TestMethod]
    public async Task GetActiveSessionForBoard_NoActiveSession_ReturnsNull()
    {
        var active = await _service.GetActiveSessionForBoardAsync(_board.Id, _admin);
        Assert.IsNull(active);
    }

    [TestMethod]
    public async Task GetActiveSessionForBoard_AfterEnd_ReturnsNull()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.EndSessionAsync(session.Id, _admin);

        var active = await _service.GetActiveSessionForBoardAsync(_board.Id, _admin);
        Assert.IsNull(active);
    }

    // ─── Entity / Data Model Assertions (Phase A) ─────────────────────

    [TestMethod]
    public async Task ReviewSession_HasCorrectDefaults()
    {
        var session = new ReviewSession
        {
            BoardId = _board.Id,
            HostUserId = _admin.UserId
        };

        Assert.AreNotEqual(Guid.Empty, session.Id);
        Assert.AreEqual(ReviewSessionStatus.Active, session.Status);
        Assert.IsNull(session.CurrentCardId);
        Assert.IsNull(session.EndedAt);
        Assert.IsTrue(session.CreatedAt <= DateTime.UtcNow);
    }

    [TestMethod]
    public async Task ReviewSessionParticipant_HasCorrectDefaults()
    {
        var participant = new ReviewSessionParticipant
        {
            ReviewSessionId = Guid.NewGuid(),
            UserId = _admin.UserId
        };

        Assert.AreNotEqual(Guid.Empty, participant.Id);
        Assert.IsTrue(participant.IsConnected);
        Assert.IsTrue(participant.JoinedAt <= DateTime.UtcNow);
    }

    [TestMethod]
    public async Task PokerSession_LinkedToReview_HasReviewSessionId()
    {
        var session = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.SetCurrentCardAsync(session.Id, _card.Id, _admin);
        var updated = await _service.StartPokerForCurrentCardAsync(session.Id,
            new StartReviewPokerDto(), _admin);

        // Verify the poker session in the DB has the ReviewSessionId FK set
        var pokerInDb = await _db.PokerSessions.FindAsync(updated.ActivePokerSession!.Id);
        Assert.IsNotNull(pokerInDb);
        Assert.AreEqual(session.Id, pokerInDb.ReviewSessionId);
    }

    // ─── StartSession allows new session after previous ended ─────────

    [TestMethod]
    public async Task StartSession_AfterPreviousEnded_Succeeds()
    {
        var first = await _service.StartSessionAsync(_board.Id, _admin);
        await _service.EndSessionAsync(first.Id, _admin);

        var second = await _service.StartSessionAsync(_board.Id, _admin);

        Assert.IsNotNull(second);
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreEqual(ReviewSessionStatus.Active, second.Status);
    }
}
