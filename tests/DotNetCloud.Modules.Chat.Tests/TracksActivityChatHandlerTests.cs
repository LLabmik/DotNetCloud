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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
            ProductId = Guid.CreateVersion7(),
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
        var productId = Guid.CreateVersion7();
        var evt = new WorkItemCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
            Type = WorkItemType.Item,
            FromSwimlaneId = Guid.CreateVersion7(),
            ToSwimlaneId = Guid.CreateVersion7()
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
        var userId = Guid.CreateVersion7();
        var evt = new WorkItemAssignedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7()
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.CreateVersion7(),
            EpicId = Guid.CreateVersion7()
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.CreateVersion7(),
            EpicId = Guid.CreateVersion7()
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            CommentId = Guid.CreateVersion7(),
            WorkItemId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7()
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ProductId = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            OwnerId = Guid.CreateVersion7()
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ProductId = Guid.CreateVersion7()
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
            ProductId = Guid.CreateVersion7(),
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
            ProductId = Guid.CreateVersion7(),
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
