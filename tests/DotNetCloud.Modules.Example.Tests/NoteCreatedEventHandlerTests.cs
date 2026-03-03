using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Example.Events;
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
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.NewGuid(),
            Title = "Test Note",
            CreatedByUserId = Guid.NewGuid()
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
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.NewGuid(),
            Title = "Logged Note",
            CreatedByUserId = Guid.NewGuid()
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
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.NewGuid(),
            Title = "Test",
            CreatedByUserId = Guid.NewGuid()
        };

        using var cts = new CancellationTokenSource();
        await handler.HandleAsync(@event, cts.Token);
    }
}
