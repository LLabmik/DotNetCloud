using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="TracksActivityChatHandler"/> verifying Tracks → Chat real-time integration.
/// </summary>
[TestClass]
public class TracksActivityChatHandlerTests
{
    private Mock<IRealtimeBroadcaster> _broadcasterMock = null!;
    private TracksActivityChatHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _broadcasterMock = new Mock<IRealtimeBroadcaster>();
        _handler = new TracksActivityChatHandler(
            NullLogger<TracksActivityChatHandler>.Instance,
            _broadcasterMock.Object);
    }

    [TestMethod]
    public void ImplementsAllTracksEventHandlerInterfaces()
    {
        Assert.IsInstanceOfType<IEventHandler<CardCreatedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<CardMovedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<CardUpdatedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<CardDeletedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<CardAssignedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<CardCommentAddedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<SprintStartedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<SprintCompletedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<BoardCreatedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<BoardDeletedEvent>>(_handler);
    }

    [TestMethod]
    public async Task HandleCardCreatedEvent_BroadcastsToActivityGroup()
    {
        var evt = new CardCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            Title = "Test Card",
            BoardId = Guid.NewGuid(),
            SwimlaneId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "tracks-activity",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleCardCreatedEvent_BroadcastsToBoardGroup()
    {
        var boardId = Guid.NewGuid();
        var evt = new CardCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            Title = "Test Card",
            BoardId = boardId,
            SwimlaneId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            $"tracks-board-chat-{boardId}",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleCardMovedEvent_BroadcastsActivity()
    {
        var evt = new CardMovedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            FromSwimlaneId = Guid.NewGuid(),
            ToSwimlaneId = Guid.NewGuid(),
            MovedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "tracks-activity",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleCardAssignedEvent_SendsDirectNotificationToAssignee()
    {
        var assignedUserId = Guid.NewGuid();
        var evt = new CardAssignedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            AssignedUserId = assignedUserId,
            AssignedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            assignedUserId,
            "TracksCardAssignedToYou",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleCardAssignedEvent_BroadcastsToBoardGroup()
    {
        var boardId = Guid.NewGuid();
        var evt = new CardAssignedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            BoardId = boardId,
            AssignedUserId = Guid.NewGuid(),
            AssignedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            $"tracks-board-chat-{boardId}",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleSprintStartedEvent_BroadcastsActivity()
    {
        var evt = new SprintStartedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "Sprint 1",
            StartedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "tracks-activity",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleSprintCompletedEvent_BroadcastsActivity()
    {
        var evt = new SprintCompletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "Sprint 1",
            CompletedByUserId = Guid.NewGuid(),
            CompletedCardCount = 5,
            TotalCardCount = 8
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "tracks-activity",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleCardUpdatedEvent_BroadcastsActivity()
    {
        var evt = new CardUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            UpdatedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "tracks-activity",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleCardDeletedEvent_BroadcastsActivity()
    {
        var evt = new CardDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            DeletedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "tracks-activity",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleCardCommentAddedEvent_BroadcastsActivity()
    {
        var evt = new CardCommentAddedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CommentId = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "tracks-activity",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleBoardCreatedEvent_BroadcastsActivity()
    {
        var evt = new BoardCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            BoardId = Guid.NewGuid(),
            Title = "New Board",
            OwnerId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "tracks-activity",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleBoardDeletedEvent_BroadcastsActivity()
    {
        var evt = new BoardDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            BoardId = Guid.NewGuid(),
            DeletedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "tracks-activity",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task NullBroadcaster_HandlesEventsWithoutThrowing()
    {
        var handler = new TracksActivityChatHandler(
            NullLogger<TracksActivityChatHandler>.Instance,
            broadcaster: null);

        var evt = new CardCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            Title = "Test",
            BoardId = Guid.NewGuid(),
            SwimlaneId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid()
        };

        // Should not throw even without a broadcaster
        await handler.HandleAsync(evt);
    }

    [TestMethod]
    public async Task HandleCardCreatedEvent_SupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var evt = new CardCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            Title = "Test",
            BoardId = Guid.NewGuid(),
            SwimlaneId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt, cts.Token);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object>(),
            cts.Token), Times.Exactly(2)); // Global + board-specific
    }
}
