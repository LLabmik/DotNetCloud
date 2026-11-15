using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Data.Services;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Contacts.Tests;

/// <summary>
/// Tests for <see cref="ContactRelatedEntitiesService"/> — the reverse lookups the contact detail
/// page shows ("upcoming events" and "linked notes").
/// </summary>
/// <remarks>
/// The de-duplication in this service used to run <c>DISTINCT</c> over the whole matched entity.
/// On SQL Server that failed outright once a column was a <c>text</c> column (SQL Server refuses to
/// compare <c>text</c>), so opening any contact whose email matched an invited event — or that had a
/// linked note — answered 500. These tests pin the behaviour the fixed query must keep: one entry per
/// event/note, newest first, capped at 50.
/// </remarks>
[TestClass]
public class ContactRelatedEntitiesServiceTests
{
    private ContactsDbContext _contactsDb = null!;
    private CalendarDbContext _calendarDb = null!;
    private NotesDbContext _notesDb = null!;
    private ContactRelatedEntitiesService _service = null!;
    private Guid _userId;
    private Contact _alice = null!;

    [TestInitialize]
    public void Setup()
    {
        _contactsDb = new ContactsDbContext(NewOptions<ContactsDbContext>());
        _calendarDb = new CalendarDbContext(NewOptions<CalendarDbContext>());
        _notesDb = new NotesDbContext(NewOptions<NotesDbContext>());
        _service = new ContactRelatedEntitiesService(_contactsDb, _calendarDb, _notesDb);
        _userId = Guid.CreateVersion7();

        _alice = new Contact
        {
            Id = Guid.CreateVersion7(),
            OwnerId = _userId,
            DisplayName = "Alice Johnson",
            FirstName = "Alice",
            LastName = "Johnson",
            ContactType = ContactType.Person,
            Emails =
            [
                new ContactEmail { Address = "Alice@Work.com", Label = "work", IsPrimary = true, SortOrder = 0 },
                new ContactEmail { Address = "alice@home.net", Label = "home", SortOrder = 1 }
            ]
        };

        _contactsDb.Contacts.Add(_alice);
        _contactsDb.SaveChanges();
    }

    /// <summary>
    /// One event invited to both of the contact's addresses produces two attendee rows. The card must
    /// show the event once — this is the duplicate that made the query need DISTINCT in the first place.
    /// </summary>
    [TestMethod]
    public async Task GetRelatedAsync_EventInvitedToTwoContactAddresses_ReturnsOneEntryPerEvent()
    {
        var shared = NewEvent("Sprint review", DateTime.UtcNow.AddDays(1));
        _calendarDb.CalendarEvents.Add(shared);
        AddAttendee(shared, "alice@work.com");
        AddAttendee(shared, "ALICE@HOME.NET");

        var other = NewEvent("Standup", DateTime.UtcNow.AddDays(2));
        _calendarDb.CalendarEvents.Add(other);
        AddAttendee(other, "bob@example.com");
        _calendarDb.SaveChanges();

        var result = await _service.GetRelatedAsync(_alice.Id, _userId);

        Assert.AreEqual(1, result.Events.Count, "the event is invited to both addresses but must be listed once");
        Assert.AreEqual(shared.Id, result.Events[0].Id);
        Assert.AreEqual("Sprint review", result.Events[0].Title);
    }

    /// <summary>
    /// Events are newest-first so the card shows the most relevant first, and the newest of the
    /// matching events leads.
    /// </summary>
    [TestMethod]
    public async Task GetRelatedAsync_MatchingEvents_OrderedByStartDescending()
    {
        var older = NewEvent("Last week", DateTime.UtcNow.AddDays(-7));
        _calendarDb.CalendarEvents.Add(older);
        AddAttendee(older, "alice@home.net");
        var newer = NewEvent("Tomorrow", DateTime.UtcNow.AddDays(1));
        _calendarDb.CalendarEvents.Add(newer);
        AddAttendee(newer, "alice@work.com");
        _calendarDb.SaveChanges();

        var result = await _service.GetRelatedAsync(_alice.Id, _userId);

        CollectionAssert.AreEqual(
            new[] { newer.Id, older.Id },
            result.Events.Select(e => e.Id).ToArray());
    }

    /// <summary>
    /// Notes linked to the contact are listed once each. The note body is deliberately long — it is
    /// the column that could not participate in a DISTINCT on SQL Server.
    /// </summary>
    [TestMethod]
    public async Task GetRelatedAsync_NotesLinkedToContact_ReturnsEachNoteOnce()
    {
        var note = new Note
        {
            Id = Guid.CreateVersion7(),
            OwnerId = _userId,
            Title = "Follow up with Alice",
            Content = new string('x', 5_000),
            UpdatedAt = DateTime.UtcNow
        };
        _notesDb.Notes.Add(note);
        _notesDb.NoteLinks.AddRange(
            new NoteLink { NoteId = note.Id, LinkType = NoteLinkType.Contact, TargetId = _alice.Id },
            new NoteLink { NoteId = note.Id, LinkType = NoteLinkType.Contact, TargetId = _alice.Id });
        _notesDb.NoteLinks.Add(new NoteLink
        {
            NoteId = note.Id,
            LinkType = NoteLinkType.Contact,
            TargetId = Guid.CreateVersion7()
        });
        _notesDb.SaveChanges();

        var result = await _service.GetRelatedAsync(_alice.Id, _userId);

        Assert.AreEqual(1, result.Notes.Count);
        Assert.AreEqual(note.Id, result.Notes[0].Id);
        Assert.AreEqual("Follow up with Alice", result.Notes[0].Title);
    }

    /// <summary>
    /// An unknown contact (or one owned by somebody else) yields an empty card rather than an error.
    /// </summary>
    [TestMethod]
    public async Task GetRelatedAsync_UnknownContact_ReturnsEmpty()
    {
        var result = await _service.GetRelatedAsync(Guid.CreateVersion7(), _userId);

        Assert.AreEqual(0, result.Events.Count);
        Assert.AreEqual(0, result.Notes.Count);
    }

    /// <summary>
    /// A contact with no addresses cannot match an attendee, so the calendar context is not queried.
    /// </summary>
    [TestMethod]
    public async Task GetRelatedAsync_ContactWithoutEmail_ReturnsNoEvents()
    {
        var noEmail = new Contact
        {
            Id = Guid.CreateVersion7(),
            OwnerId = _userId,
            DisplayName = "No Email",
            ContactType = ContactType.Person
        };
        _contactsDb.Contacts.Add(noEmail);
        _contactsDb.SaveChanges();

        var matching = NewEvent("Invited", DateTime.UtcNow.AddDays(1));
        _calendarDb.CalendarEvents.Add(matching);
        AddAttendee(matching, "alice@work.com");
        _calendarDb.SaveChanges();

        var result = await _service.GetRelatedAsync(noEmail.Id, _userId);

        Assert.AreEqual(0, result.Events.Count);
    }

    private static DbContextOptions<T> NewOptions<T>()
        where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase($"{typeof(T).Name}-{Guid.CreateVersion7()}")
            .Options;

    private CalendarEvent NewEvent(string title, DateTime startUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        CalendarId = Guid.CreateVersion7(),
        CreatedByUserId = _userId,
        Title = title,
        // A long description mirrors the production rows; it is one of the columns the old query
        // asked SQL Server to compare.
        Description = new string('d', 2_000),
        StartUtc = startUtc,
        EndUtc = startUtc.AddHours(1)
    };

    private void AddAttendee(CalendarEvent calendarEvent, string email)
    {
        _calendarDb.EventAttendees.Add(new EventAttendee
        {
            Id = Guid.CreateVersion7(),
            EventId = calendarEvent.Id,
            Email = email
        });
    }
}
