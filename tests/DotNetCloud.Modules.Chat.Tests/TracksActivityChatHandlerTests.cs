using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
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
        Assert.IsInstanceOfType<IEventHandler<WorkItemCreatedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<WorkItemMovedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<WorkItemUpdatedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<WorkItemDeletedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<WorkItemAssignedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<WorkItemCommentAddedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<SprintStartedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<SprintCompletedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<ProductCreatedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<ProductDeletedEvent>>(_handler);
    }

    [TestMethod]
    public async Task HandleCardCreatedEvent_BroadcastsToActivityGroup()
    {
        var evt = new WorkItemCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Type = WorkItemType.Item
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "tracks-activity",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleCardCreatedEvent_BroadcastsToProductGroup()
    {
        var productId = Guid.NewGuid();
        var evt = new WorkItemCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            ProductId = productId,
            Type = WorkItemType.Item
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            $"tracks-product-chat-{productId}",
            "TracksActivityNotification",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleCardMovedEvent_BroadcastsActivity()
    {
        var evt = new WorkItemMovedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            Type = WorkItemType.Item,
            FromSwimlaneId = Guid.NewGuid(),
            ToSwimlaneId = Guid.NewGuid()
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
        var userId = Guid.NewGuid();
        var evt = new WorkItemAssignedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            UserId = userId
        };

        await _handler.HandleAsync(evt);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            userId,
            "TracksWorkItemAssignedToYou",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleCardAssignedEvent_BroadcastsToActivityGroup()
    {
        var evt = new WorkItemAssignedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
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
    public async Task HandleSprintStartedEvent_BroadcastsActivity()
    {
        var evt = new SprintStartedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.NewGuid(),
            EpicId = Guid.NewGuid()
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
            EpicId = Guid.NewGuid()
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
        var evt = new WorkItemUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            Type = WorkItemType.Item
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
        var evt = new WorkItemDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            Type = WorkItemType.Item
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
        var evt = new WorkItemCommentAddedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CommentId = Guid.NewGuid(),
            WorkItemId = Guid.NewGuid(),
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
        var evt = new ProductCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ProductId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
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
        var evt = new ProductDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ProductId = Guid.NewGuid()
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

        var evt = new WorkItemCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Type = WorkItemType.Item
        };

        // Should not throw even without a broadcaster
        await handler.HandleAsync(evt);
    }

    [TestMethod]
    public async Task HandleCardCreatedEvent_SupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var evt = new WorkItemCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Type = WorkItemType.Item
        };

        await _handler.HandleAsync(evt, cts.Token);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object>(),
            cts.Token), Times.Exactly(2)); // Global + product-specific
    }
}
