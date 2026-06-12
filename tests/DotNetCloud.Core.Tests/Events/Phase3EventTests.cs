namespace DotNetCloud.Core.Tests.Events;

using DotNetCloud.Core.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests for Phase 3 event types (Contacts, Calendar, Notes).
/// </summary>
[TestClass]
public class Phase3EventTests
{
    // ── Contacts ──

    [TestMethod]
    public void ContactCreatedEvent_ImplementsIEvent()
    {
        var e = new ContactCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ContactId = Guid.CreateVersion7(),
            DisplayName = "Jane Doe",
            OwnerId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.EventId);
        Assert.AreEqual("Jane Doe", e.DisplayName);
    }

    [TestMethod]
    public void ContactUpdatedEvent_ImplementsIEvent()
    {
        var e = new ContactUpdatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ContactId = Guid.CreateVersion7(),
            UpdatedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
    }

    [TestMethod]
    public void ContactDeletedEvent_ImplementsIEvent()
    {
        var e = new ContactDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ContactId = Guid.CreateVersion7(),
            DeletedByUserId = Guid.CreateVersion7(),
            IsPermanent = true
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.IsTrue(e.IsPermanent);
    }

    // ── Calendar ──

    [TestMethod]
    public void CalendarEventCreatedEvent_ImplementsIEvent()
    {
        var e = new CalendarEventCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            CalendarEventId = Guid.CreateVersion7(),
            CalendarId = Guid.CreateVersion7(),
            Title = "Standup",
            CreatedByUserId = Guid.CreateVersion7(),
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1),
            IsRecurring = true
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual("Standup", e.Title);
        Assert.IsTrue(e.IsRecurring);
    }

    [TestMethod]
    public void CalendarEventUpdatedEvent_ImplementsIEvent()
    {
        var e = new CalendarEventUpdatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            CalendarEventId = Guid.CreateVersion7(),
            CalendarId = Guid.CreateVersion7(),
            UpdatedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
    }

    [TestMethod]
    public void CalendarEventDeletedEvent_ImplementsIEvent()
    {
        var e = new CalendarEventDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            CalendarEventId = Guid.CreateVersion7(),
            CalendarId = Guid.CreateVersion7(),
            DeletedByUserId = Guid.CreateVersion7(),
            IsPermanent = false
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.IsFalse(e.IsPermanent);
    }

    [TestMethod]
    public void CalendarEventRsvpEvent_ImplementsIEvent()
    {
        var e = new CalendarEventRsvpEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            CalendarEventId = Guid.CreateVersion7(),
            AttendeeUserId = Guid.CreateVersion7(),
            Status = "Accepted"
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual("Accepted", e.Status);
    }

    [TestMethod]
    public void CalendarReminderTriggeredEvent_ImplementsIEvent()
    {
        var e = new CalendarReminderTriggeredEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            CalendarEventId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            EventTitle = "Team Sync",
            EventStartUtc = DateTime.UtcNow.AddMinutes(15)
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual("Team Sync", e.EventTitle);
    }

    // ── Notes ──

    [TestMethod]
    public void NoteCreatedEvent_ImplementsIEvent()
    {
        var e = new NoteCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.CreateVersion7(),
            Title = "Meeting Notes",
            OwnerId = Guid.CreateVersion7(),
            FolderId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual("Meeting Notes", e.Title);
        Assert.IsNotNull(e.FolderId);
    }

    [TestMethod]
    public void NoteUpdatedEvent_ImplementsIEvent()
    {
        var e = new NoteUpdatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.CreateVersion7(),
            UpdatedByUserId = Guid.CreateVersion7(),
            NewVersion = 4
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual(4, e.NewVersion);
    }

    [TestMethod]
    public void NoteDeletedEvent_ImplementsIEvent()
    {
        var e = new NoteDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            NoteId = Guid.CreateVersion7(),
            DeletedByUserId = Guid.CreateVersion7(),
            IsPermanent = true
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.IsTrue(e.IsPermanent);
    }

    // ── Immutability ──

    [TestMethod]
    public void AllPhase3Events_AreSealed()
    {
        Assert.IsTrue(typeof(ContactCreatedEvent).IsSealed);
        Assert.IsTrue(typeof(ContactUpdatedEvent).IsSealed);
        Assert.IsTrue(typeof(ContactDeletedEvent).IsSealed);
        Assert.IsTrue(typeof(CalendarEventCreatedEvent).IsSealed);
        Assert.IsTrue(typeof(CalendarEventUpdatedEvent).IsSealed);
        Assert.IsTrue(typeof(CalendarEventDeletedEvent).IsSealed);
        Assert.IsTrue(typeof(CalendarEventRsvpEvent).IsSealed);
        Assert.IsTrue(typeof(CalendarReminderTriggeredEvent).IsSealed);
        Assert.IsTrue(typeof(NoteCreatedEvent).IsSealed);
        Assert.IsTrue(typeof(NoteUpdatedEvent).IsSealed);
        Assert.IsTrue(typeof(NoteDeletedEvent).IsSealed);
    }

    [TestMethod]
    public void AllPhase3Events_AreRecords()
    {
        // Records have a compiler-generated <Clone>$ method
        Assert.IsTrue(HasCloneMethod(typeof(ContactCreatedEvent)));
        Assert.IsTrue(HasCloneMethod(typeof(CalendarEventCreatedEvent)));
        Assert.IsTrue(HasCloneMethod(typeof(NoteCreatedEvent)));
    }

    private static bool HasCloneMethod(Type type)
    {
        return type.GetMethod("<Clone>$") is not null;
    }
}
