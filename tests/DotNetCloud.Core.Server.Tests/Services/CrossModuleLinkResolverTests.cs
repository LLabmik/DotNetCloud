using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Modules.Calendar.Services;
using DotNetCloud.Modules.Contacts.Services;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class CrossModuleLinkResolverTests
{
    private Mock<IContactsApiClient> _contactsClient = null!;
    private Mock<ICalendarApiClient> _calendarClient = null!;
    private Mock<INotesApiClient> _notesClient = null!;
    private CrossModuleLinkResolver _resolver = null!;

    [TestInitialize]
    public void Setup()
    {
        _contactsClient = new Mock<IContactsApiClient>();
        _calendarClient = new Mock<ICalendarApiClient>();
        _notesClient = new Mock<INotesApiClient>();

        _resolver = new CrossModuleLinkResolver(
            NullLogger<CrossModuleLinkResolver>.Instance,
            _contactsClient.Object,
            _calendarClient.Object,
            _notesClient.Object);
    }

    // Helper factories to create DTOs with required properties filled in
    private static ContactDto CreateContact(Guid id, string displayName) => new()
    {
        Id = id,
        OwnerId = Guid.NewGuid(),
        ContactType = ContactType.Person,
        DisplayName = displayName,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static CalendarEventDto CreateEvent(Guid id, string title) => new()
    {
        Id = id,
        CalendarId = Guid.NewGuid(),
        CreatedByUserId = Guid.NewGuid(),
        Title = title,
        StartUtc = DateTime.UtcNow,
        EndUtc = DateTime.UtcNow.AddHours(1),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static NoteDto CreateNote(Guid id, string title) => new()
    {
        Id = id,
        OwnerId = Guid.NewGuid(),
        Title = title,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [TestMethod]
    public async Task ResolveAsync_Contact_ReturnsResolvedLink()
    {
        var contactId = Guid.NewGuid();
        _contactsClient.Setup(c => c.GetContactAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateContact(contactId, "Jane Doe"));

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.Contact, contactId);

        Assert.IsTrue(result.IsResolved);
        Assert.AreEqual("Jane Doe", result.DisplayLabel);
        Assert.AreEqual(CrossModuleLinkType.Contact, result.LinkType);
        Assert.AreEqual(contactId, result.TargetId);
        Assert.AreEqual($"/apps/contacts/{contactId}", result.Href);
    }

    [TestMethod]
    public async Task ResolveAsync_Contact_NotFound_ReturnsUnresolved()
    {
        var contactId = Guid.NewGuid();
        _contactsClient.Setup(c => c.GetContactAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactDto?)null);

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.Contact, contactId);

        Assert.IsFalse(result.IsResolved);
        Assert.AreEqual("[Deleted Contact]", result.DisplayLabel);
    }

    [TestMethod]
    public async Task ResolveAsync_CalendarEvent_ReturnsResolvedLink()
    {
        var eventId = Guid.NewGuid();
        _calendarClient.Setup(c => c.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEvent(eventId, "Team Standup"));

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.CalendarEvent, eventId);

        Assert.IsTrue(result.IsResolved);
        Assert.AreEqual("Team Standup", result.DisplayLabel);
        Assert.AreEqual($"/apps/calendar/event/{eventId}", result.Href);
    }

    [TestMethod]
    public async Task ResolveAsync_CalendarEvent_NotFound_ReturnsUnresolved()
    {
        var eventId = Guid.NewGuid();
        _calendarClient.Setup(c => c.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarEventDto?)null);

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.CalendarEvent, eventId);

        Assert.IsFalse(result.IsResolved);
        Assert.AreEqual("[Deleted Event]", result.DisplayLabel);
    }

    [TestMethod]
    public async Task ResolveAsync_Note_ReturnsResolvedLink()
    {
        var noteId = Guid.NewGuid();
        _notesClient.Setup(n => n.GetNoteAsync(noteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNote(noteId, "Meeting Notes"));

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.Note, noteId);

        Assert.IsTrue(result.IsResolved);
        Assert.AreEqual("Meeting Notes", result.DisplayLabel);
        Assert.AreEqual($"/apps/notes/{noteId}", result.Href);
    }

    [TestMethod]
    public async Task ResolveAsync_Note_NotFound_ReturnsUnresolved()
    {
        var noteId = Guid.NewGuid();
        _notesClient.Setup(n => n.GetNoteAsync(noteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NoteDto?)null);

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.Note, noteId);

        Assert.IsFalse(result.IsResolved);
        Assert.AreEqual("[Deleted Note]", result.DisplayLabel);
    }

    [TestMethod]
    public async Task ResolveAsync_File_ReturnsUnresolved()
    {
        // File resolution not implemented yet — should return unresolved gracefully
        var fileId = Guid.NewGuid();

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.File, fileId);

        Assert.IsFalse(result.IsResolved);
        Assert.AreEqual("[Deleted File]", result.DisplayLabel);
    }

    [TestMethod]
    public async Task ResolveAsync_ClientThrows_ReturnsUnresolved()
    {
        var contactId = Guid.NewGuid();
        _contactsClient.Setup(c => c.GetContactAsync(contactId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.Contact, contactId);

        Assert.IsFalse(result.IsResolved);
        Assert.AreEqual("[Deleted Contact]", result.DisplayLabel);
    }

    [TestMethod]
    public async Task ResolveBatchAsync_MixedTypes_ResolvesAll()
    {
        var contactId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var noteId = Guid.NewGuid();

        _contactsClient.Setup(c => c.GetContactAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateContact(contactId, "Alice"));

        _calendarClient.Setup(c => c.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEvent(eventId, "Sprint Review"));

        _notesClient.Setup(n => n.GetNoteAsync(noteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNote(noteId, "Architecture Decision"));

        var requests = new List<CrossModuleLinkRequest>
        {
            new() { LinkType = CrossModuleLinkType.Contact, TargetId = contactId },
            new() { LinkType = CrossModuleLinkType.CalendarEvent, TargetId = eventId },
            new() { LinkType = CrossModuleLinkType.Note, TargetId = noteId }
        };

        var results = await _resolver.ResolveBatchAsync(requests);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].IsResolved);
        Assert.AreEqual("Alice", results[0].DisplayLabel);
        Assert.IsTrue(results[1].IsResolved);
        Assert.AreEqual("Sprint Review", results[1].DisplayLabel);
        Assert.IsTrue(results[2].IsResolved);
        Assert.AreEqual("Architecture Decision", results[2].DisplayLabel);
    }

    [TestMethod]
    public async Task ResolveBatchAsync_SomeNotFound_MixedResults()
    {
        var foundId = Guid.NewGuid();
        var missingId = Guid.NewGuid();

        _contactsClient.Setup(c => c.GetContactAsync(foundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateContact(foundId, "Bob"));
        _contactsClient.Setup(c => c.GetContactAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactDto?)null);

        var requests = new List<CrossModuleLinkRequest>
        {
            new() { LinkType = CrossModuleLinkType.Contact, TargetId = foundId },
            new() { LinkType = CrossModuleLinkType.Contact, TargetId = missingId }
        };

        var results = await _resolver.ResolveBatchAsync(requests);

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results[0].IsResolved);
        Assert.AreEqual("Bob", results[0].DisplayLabel);
        Assert.IsFalse(results[1].IsResolved);
    }

    [TestMethod]
    public async Task ResolveBatchAsync_EmptyList_ReturnsEmpty()
    {
        var results = await _resolver.ResolveBatchAsync([]);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task ResolveBatchAsync_ClientThrows_AllUnresolved()
    {
        var contactId = Guid.NewGuid();

        _contactsClient.Setup(c => c.GetContactAsync(contactId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));

        var requests = new List<CrossModuleLinkRequest>
        {
            new() { LinkType = CrossModuleLinkType.Contact, TargetId = contactId }
        };

        var results = await _resolver.ResolveBatchAsync(requests);

        Assert.AreEqual(1, results.Count);
        Assert.IsFalse(results[0].IsResolved);
    }
}
