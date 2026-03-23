using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class CrossModuleLinkResolverTests
{
    private Mock<IContactDirectory> _contactDirectory = null!;
    private Mock<ICalendarDirectory> _calendarDirectory = null!;
    private Mock<INoteDirectory> _noteDirectory = null!;
    private CrossModuleLinkResolver _resolver = null!;

    [TestInitialize]
    public void Setup()
    {
        _contactDirectory = new Mock<IContactDirectory>();
        _calendarDirectory = new Mock<ICalendarDirectory>();
        _noteDirectory = new Mock<INoteDirectory>();

        _resolver = new CrossModuleLinkResolver(
            NullLogger<CrossModuleLinkResolver>.Instance,
            _contactDirectory.Object,
            _calendarDirectory.Object,
            _noteDirectory.Object);
    }

    [TestMethod]
    public async Task ResolveAsync_Contact_ReturnsResolvedLink()
    {
        var contactId = Guid.NewGuid();
        _contactDirectory.Setup(c => c.GetContactDisplayNameAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Jane Doe");

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
        _contactDirectory.Setup(c => c.GetContactDisplayNameAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.Contact, contactId);

        Assert.IsFalse(result.IsResolved);
        Assert.AreEqual("[Deleted Contact]", result.DisplayLabel);
    }

    [TestMethod]
    public async Task ResolveAsync_CalendarEvent_ReturnsResolvedLink()
    {
        var eventId = Guid.NewGuid();
        _calendarDirectory.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CalendarEventSummary
            {
                Id = eventId,
                Title = "Team Standup",
                StartUtc = DateTime.UtcNow,
                EndUtc = DateTime.UtcNow.AddHours(1)
            });

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.CalendarEvent, eventId);

        Assert.IsTrue(result.IsResolved);
        Assert.AreEqual("Team Standup", result.DisplayLabel);
        Assert.AreEqual($"/apps/calendar/event/{eventId}", result.Href);
    }

    [TestMethod]
    public async Task ResolveAsync_CalendarEvent_NotFound_ReturnsUnresolved()
    {
        var eventId = Guid.NewGuid();
        _calendarDirectory.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarEventSummary?)null);

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.CalendarEvent, eventId);

        Assert.IsFalse(result.IsResolved);
        Assert.AreEqual("[Deleted Event]", result.DisplayLabel);
    }

    [TestMethod]
    public async Task ResolveAsync_Note_ReturnsResolvedLink()
    {
        var noteId = Guid.NewGuid();
        _noteDirectory.Setup(n => n.GetNoteTitleAsync(noteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Meeting Notes");

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.Note, noteId);

        Assert.IsTrue(result.IsResolved);
        Assert.AreEqual("Meeting Notes", result.DisplayLabel);
        Assert.AreEqual($"/apps/notes/{noteId}", result.Href);
    }

    [TestMethod]
    public async Task ResolveAsync_Note_NotFound_ReturnsUnresolved()
    {
        var noteId = Guid.NewGuid();
        _noteDirectory.Setup(n => n.GetNoteTitleAsync(noteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

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
    public async Task ResolveAsync_DirectoryThrows_ReturnsUnresolved()
    {
        var contactId = Guid.NewGuid();
        _contactDirectory.Setup(c => c.GetContactDisplayNameAsync(contactId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var result = await _resolver.ResolveAsync(CrossModuleLinkType.Contact, contactId);

        Assert.IsFalse(result.IsResolved);
        Assert.AreEqual("[Deleted Contact]", result.DisplayLabel);
    }

    [TestMethod]
    public async Task ResolveAsync_NullDirectory_ReturnsUnresolved()
    {
        var resolver = new CrossModuleLinkResolver(
            NullLogger<CrossModuleLinkResolver>.Instance);

        var result = await resolver.ResolveAsync(CrossModuleLinkType.Contact, Guid.NewGuid());

        Assert.IsFalse(result.IsResolved);
    }

    [TestMethod]
    public async Task ResolveBatchAsync_MixedTypes_ResolvesAll()
    {
        var contactId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var noteId = Guid.NewGuid();

        _contactDirectory.Setup(c => c.GetContactDisplayNamesAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [contactId] = "Alice" });

        _calendarDirectory.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CalendarEventSummary
            {
                Id = eventId,
                Title = "Sprint Review",
                StartUtc = DateTime.UtcNow,
                EndUtc = DateTime.UtcNow.AddHours(1)
            });

        _noteDirectory.Setup(n => n.GetNoteTitlesAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [noteId] = "Architecture Decision" });

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

        _contactDirectory.Setup(c => c.GetContactDisplayNamesAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [foundId] = "Bob" });

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
    public async Task ResolveBatchAsync_BatchDirectoryThrows_AllUnresolved()
    {
        var contactId = Guid.NewGuid();

        _contactDirectory.Setup(c => c.GetContactDisplayNamesAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
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
