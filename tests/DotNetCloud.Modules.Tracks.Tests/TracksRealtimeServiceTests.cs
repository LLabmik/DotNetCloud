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
    }
}
