using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class TracksRealtimeServiceTests
{
    private Mock<IRealtimeBroadcaster> _broadcaster = null!;
    private ITracksRealtimeService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _broadcaster = new Mock<IRealtimeBroadcaster>();
        _service = new TracksRealtimeService(
            NullLogger<TracksRealtimeService>.Instance,
            _broadcaster.Object);
    }

    [TestMethod]
    public async Task BroadcastCardAction_SendsToBoardGroup()
    {
        var boardId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        await _service.BroadcastCardActionAsync(boardId, cardId, "created");

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-board-{boardId}",
            "TracksCardAction",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BroadcastSwimlaneAction_SendsToBoardGroup()
    {
        var boardId = Guid.NewGuid();
        var swimlaneId = Guid.NewGuid();

        await _service.BroadcastSwimlaneActionAsync(boardId, swimlaneId, "created");

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-board-{boardId}",
            "TracksSwimlaneAction",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BroadcastCommentAction_SendsToBoardGroup()
    {
        var boardId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var commentId = Guid.NewGuid();

        await _service.BroadcastCommentActionAsync(boardId, cardId, commentId, "added");

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-board-{boardId}",
            "TracksCommentAction",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BroadcastSprintAction_SendsToBoardGroup()
    {
        var boardId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();

        await _service.BroadcastSprintActionAsync(boardId, sprintId, "started");

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-board-{boardId}",
            "TracksSprintAction",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BroadcastActivity_SendsToBoardGroup()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _service.BroadcastActivityAsync(boardId, userId, "card_created", "Card", Guid.NewGuid());

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-board-{boardId}",
            "TracksActivity",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BroadcastBoardMemberAction_SendsToBoardGroup()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _service.BroadcastBoardMemberActionAsync(boardId, userId, "added");

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-board-{boardId}",
            "TracksBoardMemberAction",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BroadcastTeamAction_SendsToTeamGroup()
    {
        var teamId = Guid.NewGuid();

        await _service.BroadcastTeamActionAsync(teamId, "created");

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-team-{teamId}",
            "TracksTeamAction",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task AddUserToBoardGroup_CallsBroadcaster()
    {
        var userId = Guid.NewGuid();
        var boardId = Guid.NewGuid();

        await _service.AddUserToBoardGroupAsync(userId, boardId);

        _broadcaster.Verify(b => b.AddToGroupAsync(
            userId,
            $"tracks-board-{boardId}",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RemoveUserFromBoardGroup_CallsBroadcaster()
    {
        var userId = Guid.NewGuid();
        var boardId = Guid.NewGuid();

        await _service.RemoveUserFromBoardGroupAsync(userId, boardId);

        _broadcaster.Verify(b => b.RemoveFromGroupAsync(
            userId,
            $"tracks-board-{boardId}",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task NoBroadcaster_AllMethodsAreNoOps()
    {
        ITracksRealtimeService service = new TracksRealtimeService(NullLogger<TracksRealtimeService>.Instance);
        var boardId = Guid.NewGuid();

        // These should all complete without throwing
        await service.BroadcastCardActionAsync(boardId, Guid.NewGuid(), "created");
        await service.BroadcastSwimlaneActionAsync(boardId, Guid.NewGuid(), "created");
        await service.BroadcastCommentActionAsync(boardId, Guid.NewGuid(), Guid.NewGuid(), "added");
        await service.BroadcastSprintActionAsync(boardId, Guid.NewGuid(), "started");
        await service.BroadcastActivityAsync(boardId, Guid.NewGuid(), "test", "Board", Guid.NewGuid());
        await service.BroadcastBoardMemberActionAsync(boardId, Guid.NewGuid(), "added");
        await service.BroadcastTeamActionAsync(Guid.NewGuid(), "created");
        await service.AddUserToBoardGroupAsync(Guid.NewGuid(), boardId);
        await service.RemoveUserFromBoardGroupAsync(Guid.NewGuid(), boardId);
        // Phase D review methods
        await service.BroadcastReviewCardChangedAsync(Guid.NewGuid(), boardId, Guid.NewGuid());
        await service.BroadcastReviewSessionStateAsync(Guid.NewGuid(), boardId, "started");
        await service.BroadcastPokerVoteStatusAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true);
        await service.BroadcastReviewPokerStateAsync(Guid.NewGuid(), Guid.NewGuid(), boardId, "revealed");
        await service.BroadcastReviewParticipantChangedAsync(Guid.NewGuid(), Guid.NewGuid(), "joined");
        await service.AddUserToReviewGroupAsync(Guid.NewGuid(), Guid.NewGuid());
        await service.RemoveUserFromReviewGroupAsync(Guid.NewGuid(), Guid.NewGuid());
    }

    // ─── Review Broadcast Methods (Phase D) ──────────────────────────

    [TestMethod]
    public async Task BroadcastReviewCardChanged_SendsToReviewGroup()
    {
        var sessionId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        await _service.BroadcastReviewCardChangedAsync(sessionId, boardId, cardId);

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-review-{sessionId}",
            "TracksReviewCardChanged",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BroadcastReviewSessionState_SendsToBothGroups()
    {
        var sessionId = Guid.NewGuid();
        var boardId = Guid.NewGuid();

        await _service.BroadcastReviewSessionStateAsync(sessionId, boardId, "started");

        // Should broadcast to both review group and board group
        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-review-{sessionId}",
            "TracksReviewSessionState",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-board-{boardId}",
            "TracksReviewSessionState",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BroadcastPokerVoteStatus_SendsToReviewGroup()
    {
        var sessionId = Guid.NewGuid();
        var pokerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _service.BroadcastPokerVoteStatusAsync(sessionId, pokerId, userId, true);

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-review-{sessionId}",
            "TracksPokerVoteStatus",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BroadcastReviewPokerState_SendsToReviewGroup()
    {
        var sessionId = Guid.NewGuid();
        var pokerId = Guid.NewGuid();
        var boardId = Guid.NewGuid();

        await _service.BroadcastReviewPokerStateAsync(sessionId, pokerId, boardId, "revealed");

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-review-{sessionId}",
            "TracksReviewPokerState",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task BroadcastReviewParticipantChanged_SendsToReviewGroup()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _service.BroadcastReviewParticipantChangedAsync(sessionId, userId, "joined");

        _broadcaster.Verify(b => b.BroadcastAsync(
            $"tracks-review-{sessionId}",
            "TracksReviewParticipantChanged",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task AddUserToReviewGroup_CallsBroadcaster()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await _service.AddUserToReviewGroupAsync(userId, sessionId);

        _broadcaster.Verify(b => b.AddToGroupAsync(
            userId,
            $"tracks-review-{sessionId}",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RemoveUserFromReviewGroup_CallsBroadcaster()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await _service.RemoveUserFromReviewGroupAsync(userId, sessionId);

        _broadcaster.Verify(b => b.RemoveFromGroupAsync(
            userId,
            $"tracks-review-{sessionId}",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
