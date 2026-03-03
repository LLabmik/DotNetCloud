using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Example.Events;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Tests for <see cref="NoteCreatedEvent"/> and <see cref="NoteDeletedEvent"/> domain events.
/// </summary>
[TestClass]
public class EventTests
{
    // ---- NoteCreatedEvent ----

    [TestMethod]
    public void WhenNoteCreatedEventCreatedThenImplementsIEvent()
    {
        var @event = new NoteCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.NewGuid(),
            Title = "Test",
            CreatedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType<IEvent>(@event);
    }

    [TestMethod]
    public void WhenNoteCreatedEventCreatedThenPropertiesAreSet()
    {
        var eventId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var @event = new NoteCreatedEvent
        {
            EventId = eventId,
            CreatedAt = now,
            NoteId = noteId,
            Title = "My Note",
            CreatedByUserId = userId
        };

        Assert.AreEqual(eventId, @event.EventId);
        Assert.AreEqual(now, @event.CreatedAt);
        Assert.AreEqual(noteId, @event.NoteId);
        Assert.AreEqual("My Note", @event.Title);
        Assert.AreEqual(userId, @event.CreatedByUserId);
    }

    [TestMethod]
    public void WhenNoteCreatedEventCreatedThenIsRecord()
    {
        var original = new NoteCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.NewGuid(),
            Title = "Original",
            CreatedByUserId = Guid.NewGuid()
        };

        var copy = original with { Title = "Copy" };

        Assert.AreEqual("Original", original.Title);
        Assert.AreEqual("Copy", copy.Title);
        Assert.AreEqual(original.EventId, copy.EventId);
    }

    // ---- NoteDeletedEvent ----

    [TestMethod]
    public void WhenNoteDeletedEventCreatedThenImplementsIEvent()
    {
        var @event = new NoteDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.NewGuid(),
            DeletedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType<IEvent>(@event);
    }

    [TestMethod]
    public void WhenNoteDeletedEventCreatedThenPropertiesAreSet()
    {
        var eventId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var @event = new NoteDeletedEvent
        {
            EventId = eventId,
            CreatedAt = now,
            NoteId = noteId,
            DeletedByUserId = userId
        };

        Assert.AreEqual(eventId, @event.EventId);
        Assert.AreEqual(now, @event.CreatedAt);
        Assert.AreEqual(noteId, @event.NoteId);
        Assert.AreEqual(userId, @event.DeletedByUserId);
    }
}
