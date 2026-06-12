using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for all Chat event records verifying IEvent interface compliance and record semantics.
/// </summary>
[TestClass]
public class EventTests
{
    private static readonly Guid TestEventId = Guid.CreateVersion7();
    private static readonly DateTime TestTime = DateTime.UtcNow;

    [TestMethod]
    public void MessageSentEventImplementsIEvent()
    {
        var evt = new MessageSentEvent
        {
            EventId = TestEventId,
            CreatedAt = TestTime,
            MessageId = Guid.CreateVersion7(),
            ChannelId = Guid.CreateVersion7(),
            SenderUserId = Guid.CreateVersion7(),
            Content = "Hello",
            MessageType = "Text"
        };

        Assert.IsInstanceOfType<IEvent>(evt);
        Assert.AreEqual(TestEventId, evt.EventId);
        Assert.AreEqual(TestTime, evt.CreatedAt);
    }

    [TestMethod]
    public void MessageEditedEventImplementsIEvent()
    {
        var evt = new MessageEditedEvent
        {
            EventId = TestEventId,
            CreatedAt = TestTime,
            MessageId = Guid.CreateVersion7(),
            ChannelId = Guid.CreateVersion7(),
            EditedByUserId = Guid.CreateVersion7(),
            NewContent = "Updated"
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void MessageDeletedEventImplementsIEvent()
    {
        var evt = new MessageDeletedEvent
        {
            EventId = TestEventId,
            CreatedAt = TestTime,
            MessageId = Guid.CreateVersion7(),
            ChannelId = Guid.CreateVersion7(),
            DeletedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void ChannelCreatedEventImplementsIEvent()
    {
        var evt = new ChannelCreatedEvent
        {
            EventId = TestEventId,
            CreatedAt = TestTime,
            ChannelId = Guid.CreateVersion7(),
            ChannelName = "general",
            ChannelType = "Public",
            CreatedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void ChannelDeletedEventImplementsIEvent()
    {
        var evt = new ChannelDeletedEvent
        {
            EventId = TestEventId,
            CreatedAt = TestTime,
            ChannelId = Guid.CreateVersion7(),
            ChannelName = "old-channel",
            DeletedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void ChannelArchivedEventImplementsIEvent()
    {
        var evt = new ChannelArchivedEvent
        {
            EventId = TestEventId,
            CreatedAt = TestTime,
            ChannelId = Guid.CreateVersion7(),
            ChannelName = "archived-channel",
            ArchivedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void UserJoinedChannelEventImplementsIEvent()
    {
        var evt = new UserJoinedChannelEvent
        {
            EventId = TestEventId,
            CreatedAt = TestTime,
            ChannelId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            AddedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void UserLeftChannelEventImplementsIEvent()
    {
        var evt = new UserLeftChannelEvent
        {
            EventId = TestEventId,
            CreatedAt = TestTime,
            ChannelId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            RemovedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void ReactionAddedEventImplementsIEvent()
    {
        var evt = new ReactionAddedEvent
        {
            EventId = TestEventId,
            CreatedAt = TestTime,
            MessageId = Guid.CreateVersion7(),
            ChannelId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            Emoji = "👍"
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void ReactionRemovedEventImplementsIEvent()
    {
        var evt = new ReactionRemovedEvent
        {
            EventId = TestEventId,
            CreatedAt = TestTime,
            MessageId = Guid.CreateVersion7(),
            ChannelId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            Emoji = "👍"
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }
}

/// <summary>
/// Tests for <see cref="MessageSentEventHandler"/> and <see cref="ChannelCreatedEventHandler"/>.
/// </summary>
[TestClass]
public class EventHandlerTests
{
    [TestMethod]
    public void MessageSentEventHandlerImplementsIEventHandler()
    {
        var handler = new MessageSentEventHandler(NullLogger<MessageSentEventHandler>.Instance);
        Assert.IsInstanceOfType<IEventHandler<MessageSentEvent>>(handler);
    }

    [TestMethod]
    public async Task MessageSentEventHandlerHandlesEventWithoutThrowing()
    {
        var handler = new MessageSentEventHandler(NullLogger<MessageSentEventHandler>.Instance);
        var evt = new MessageSentEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            MessageId = Guid.CreateVersion7(),
            ChannelId = Guid.CreateVersion7(),
            SenderUserId = Guid.CreateVersion7(),
            Content = "test message",
            MessageType = "Text"
        };

        await handler.HandleAsync(evt);
    }

    [TestMethod]
    public void ChannelCreatedEventHandlerImplementsIEventHandler()
    {
        var handler = new ChannelCreatedEventHandler(new Mock<IChatMessageNotifier>().Object, NullLogger<ChannelCreatedEventHandler>.Instance);
        Assert.IsInstanceOfType<IEventHandler<ChannelCreatedEvent>>(handler);
    }

    [TestMethod]
    public async Task ChannelCreatedEventHandlerHandlesEventWithoutThrowing()
    {
        var handler = new ChannelCreatedEventHandler(new Mock<IChatMessageNotifier>().Object, NullLogger<ChannelCreatedEventHandler>.Instance);
        var evt = new ChannelCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ChannelId = Guid.CreateVersion7(),
            ChannelName = "test-channel",
            ChannelType = "Public",
            CreatedByUserId = Guid.CreateVersion7()
        };

        await handler.HandleAsync(evt);
    }

    [TestMethod]
    public async Task MessageSentEventHandlerSupportsCancellation()
    {
        var handler = new MessageSentEventHandler(NullLogger<MessageSentEventHandler>.Instance);
        using var cts = new CancellationTokenSource();
        var evt = new MessageSentEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            MessageId = Guid.CreateVersion7(),
            ChannelId = Guid.CreateVersion7(),
            SenderUserId = Guid.CreateVersion7(),
            Content = "test",
            MessageType = "Text"
        };

        await handler.HandleAsync(evt, cts.Token);
    }

    [TestMethod]
    public async Task ChannelCreatedEventHandlerSupportsCancellation()
    {
        var handler = new ChannelCreatedEventHandler(new Mock<IChatMessageNotifier>().Object, NullLogger<ChannelCreatedEventHandler>.Instance);
        using var cts = new CancellationTokenSource();
        var evt = new ChannelCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ChannelId = Guid.CreateVersion7(),
            ChannelName = "test",
            ChannelType = "Public",
            CreatedByUserId = Guid.CreateVersion7()
        };

        await handler.HandleAsync(evt, cts.Token);
    }
}
