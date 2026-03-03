namespace DotNetCloud.Core.Tests.Events;

using DotNetCloud.Core.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>
/// Tests for event system interfaces.
/// </summary>
[TestClass]
public class EventSystemTests
{
    /// <summary>
    /// Test event implementation for testing.
    /// </summary>
    private class TestEvent : IEvent
    {
        public Guid EventId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string SourceModuleId { get; set; } = string.Empty;

        public TestEvent()
        {
            EventId = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }

    [TestMethod]
    public void Event_ImplementsInterface()
    {
        // Arrange & Act
        var @event = new TestEvent();

        // Assert
        Assert.IsInstanceOfType(@event, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, @event.EventId);
        Assert.IsTrue(@event.CreatedAt > DateTime.MinValue);
    }

    [TestMethod]
    public void Event_EventId_IsUnique()
    {
        // Arrange & Act
        var event1 = new TestEvent();
        var event2 = new TestEvent();

        // Assert
        Assert.AreNotEqual(event1.EventId, event2.EventId);
    }

    [TestMethod]
    public void Event_CreatedAt_IsUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var @event = new TestEvent();
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.IsTrue(@event.CreatedAt >= beforeCreation && @event.CreatedAt <= afterCreation);
    }

    [TestMethod]
    public void Event_SourceModuleId_CanBeSet()
    {
        // Arrange
        var @event = new TestEvent { SourceModuleId = "test.module" };

        // Act & Assert
        Assert.AreEqual("test.module", @event.SourceModuleId);
    }

    [TestMethod]
    public void EventHandler_CanImplementInterface()
    {
        // Arrange
        var handler = new TestEventHandler();

        // Act & Assert
        Assert.IsInstanceOfType(handler, typeof(IEventHandler<TestEvent>));
    }

    [TestMethod]
    public async Task EventHandler_HandleAsync_CanBeInvoked()
    {
        // Arrange
        var handler = new TestEventHandler();
        var @event = new TestEvent();

        // Act
        await handler.HandleAsync(@event);

        // Assert
        Assert.IsTrue(handler.WasHandled);
    }

    [TestMethod]
    public async Task EventHandler_HandleAsync_ReceivesCorrectEvent()
    {
        // Arrange
        var handler = new TestEventHandler();
        var eventId = Guid.NewGuid();
        var @event = new TestEvent { EventId = eventId };

        // Act
        await handler.HandleAsync(@event);

        // Assert
        Assert.AreEqual(eventId, handler.ReceivedEventId);
    }

    [TestMethod]
    public async Task EventHandler_HandleAsync_SupportsAsync_Operations()
    {
        // Arrange
        var handler = new TestAsyncEventHandler();
        var @event = new TestEvent();

        // Act
        await handler.HandleAsync(@event);

        // Assert
        Assert.IsTrue(handler.WasHandled);
        Assert.IsTrue(handler.AsyncOperationCompleted);
    }

    [TestMethod]
    public void EventBus_CanBeMocked()
    {
        // Arrange
        var mockBus = new Mock<IEventBus>();

        // Act & Assert
        Assert.IsNotNull(mockBus.Object);
    }

    [TestMethod]
    public async Task EventBus_PublishAsync_CanBeMocked()
    {
        // Arrange
        var mockBus = new Mock<IEventBus>();
        mockBus
            .Setup(b => b.PublishAsync(It.IsAny<TestEvent>(), It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var @event = new TestEvent();
        var systemCallerId = Guid.NewGuid();
        var context = new DotNetCloud.Core.Authorization.CallerContext(
            systemCallerId, 
            Array.Empty<string>(), 
            DotNetCloud.Core.Authorization.CallerType.System);

        // Act
        await mockBus.Object.PublishAsync(@event, context);

        // Assert
        mockBus.Verify(
            b => b.PublishAsync(It.IsAny<TestEvent>(), It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task EventBus_SubscribeAsync_CanBeMocked()
    {
        // Arrange
        var mockBus = new Mock<IEventBus>();
        mockBus
            .Setup(b => b.SubscribeAsync(It.IsAny<IEventHandler<TestEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new TestEventHandler();

        // Act
        await mockBus.Object.SubscribeAsync(handler);

        // Assert
        mockBus.Verify(
            b => b.SubscribeAsync(It.IsAny<IEventHandler<TestEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task EventBus_UnsubscribeAsync_CanBeMocked()
    {
        // Arrange
        var mockBus = new Mock<IEventBus>();
        mockBus
            .Setup(b => b.UnsubscribeAsync(It.IsAny<IEventHandler<TestEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new TestEventHandler();

        // Act
        await mockBus.Object.UnsubscribeAsync(handler);

        // Assert
        mockBus.Verify(
            b => b.UnsubscribeAsync(It.IsAny<IEventHandler<TestEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task EventBus_MultipleHandlers_CanBeSubscribed()
    {
        // Arrange
        var mockBus = new Mock<IEventBus>();
        mockBus
            .Setup(b => b.SubscribeAsync(It.IsAny<IEventHandler<TestEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler1 = new TestEventHandler();
        var handler2 = new TestEventHandler();

        // Act
        await mockBus.Object.SubscribeAsync(handler1);
        await mockBus.Object.SubscribeAsync(handler2);

        // Assert
        mockBus.Verify(
            b => b.SubscribeAsync(It.IsAny<IEventHandler<TestEvent>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    /// <summary>
    /// Test event handler for testing.
    /// </summary>
    private class TestEventHandler : IEventHandler<TestEvent>
    {
        public bool WasHandled { get; private set; }
        public Guid ReceivedEventId { get; private set; }

        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
        {
            WasHandled = true;
            ReceivedEventId = @event.EventId;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Test event handler with async operations.
    /// </summary>
    private class TestAsyncEventHandler : IEventHandler<TestEvent>
    {
        public bool WasHandled { get; private set; }
        public bool AsyncOperationCompleted { get; private set; }

        public async Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
        {
            WasHandled = true;
            await Task.Delay(10, cancellationToken);
            AsyncOperationCompleted = true;
        }
    }
}
