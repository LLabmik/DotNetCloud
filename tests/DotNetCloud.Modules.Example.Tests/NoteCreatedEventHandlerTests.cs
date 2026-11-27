using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Example.Events;
using NoteCreatedEvent = DotNetCloud.Modules.Example.Events.NoteCreatedEvent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Tests for <see cref="NoteCreatedEventHandler"/>.
/// </summary>
[TestClass]
public class NoteCreatedEventHandlerTests
{
    [TestMethod]
    public void WhenCreatedThenImplementsIEventHandler()
    {
        var handler = new NoteCreatedEventHandler(NullLogger<NoteCreatedEventHandler>.Instance);

        Assert.IsInstanceOfType<IEventHandler<NoteCreatedEvent>>(handler);
    }

    [TestMethod]
    public async Task WhenHandledThenCompletesSuccessfully()
    {
        var handler = new NoteCreatedEventHandler(NullLogger<NoteCreatedEventHandler>.Instance);

        var @event = new NoteCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.CreateVersion7(),
            Title = "Test Note",
            CreatedByUserId = Guid.CreateVersion7()
        };

        // Should not throw
        await handler.HandleAsync(@event);
    }

    [TestMethod]
    public async Task WhenHandledThenLogsNoteCreation()
    {
        var mockLogger = new Mock<ILogger<NoteCreatedEventHandler>>();
        var handler = new NoteCreatedEventHandler(mockLogger.Object);

        var @event = new NoteCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.CreateVersion7(),
            Title = "Logged Note",
            CreatedByUserId = Guid.CreateVersion7()
        };

        await handler.HandleAsync(@event);

        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenHandledWithCancellationTokenThenCompletesSuccessfully()
    {
        var handler = new NoteCreatedEventHandler(NullLogger<NoteCreatedEventHandler>.Instance);

        var @event = new NoteCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.CreateVersion7(),
            Title = "Test",
            CreatedByUserId = Guid.CreateVersion7()
        };

        using var cts = new CancellationTokenSource();
        await handler.HandleAsync(@event, cts.Token);
    }
}
