using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Notes.Tests;

[TestClass]
public class NoteServiceTests
{
    private NotesDbContext _db;
    private NoteService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotesDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _service = new NoteService(_db, _eventBusMock.Object, NullLogger<NoteService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateNote_ValidDto_ReturnsNote()
    {
        var dto = new CreateNoteDto
        {
            Title = "Test Note",
            Content = "# Hello World",
            Format = NoteContentFormat.Markdown,
            Tags = ["work", "test"],
            Links = []
        };

        var result = await _service.CreateNoteAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Test Note", result.Title);
        Assert.AreEqual("# Hello World", result.Content);
        Assert.AreEqual(NoteContentFormat.Markdown, result.Format);
        Assert.AreEqual(_caller.UserId, result.OwnerId);
        Assert.AreEqual(1, result.Version);
        Assert.AreEqual(2, result.Tags.Count);
        Assert.IsTrue(result.Tags.Contains("work"));
        Assert.IsTrue(result.Tags.Contains("test"));
    }

    [TestMethod]
    public async Task CreateNote_WithFolder_SetsFolder()
    {
        // Create a folder first
        var folder = new Models.NoteFolder
        {
            OwnerId = _caller.UserId,
            Name = "Work"
        };
        _db.NoteFolders.Add(folder);
        await _db.SaveChangesAsync();

        var dto = new CreateNoteDto
        {
            Title = "Filed Note",
            FolderId = folder.Id
        };

        var result = await _service.CreateNoteAsync(dto, _caller);

        Assert.AreEqual(folder.Id, result.FolderId);
    }

    [TestMethod]
    public async Task CreateNote_WithInvalidFolder_Throws()
    {
        var dto = new CreateNoteDto
        {
            Title = "Bad Folder",
            FolderId = Guid.NewGuid()
        };

        var ex = await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.CreateNoteAsync(dto, _caller));

        Assert.IsTrue(ex.Message.Contains("folder", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task CreateNote_PublishesEvent()
    {
        var dto = new CreateNoteDto { Title = "Event Test" };

        await _service.CreateNoteAsync(dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<NoteCreatedEvent>(e => e.Title == "Event Test" && e.OwnerId == _caller.UserId),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreateNote_WithLinks_SetsLinks()
    {
        var targetId = Guid.NewGuid();
        var dto = new CreateNoteDto
        {
            Title = "Linked Note",
            Links = [new NoteLinkDto { LinkType = NoteLinkType.File, TargetId = targetId, DisplayLabel = "myfile.txt" }]
        };

        var result = await _service.CreateNoteAsync(dto, _caller);

        Assert.AreEqual(1, result.Links.Count);
        Assert.AreEqual(NoteLinkType.File, result.Links[0].LinkType);
        Assert.AreEqual(targetId, result.Links[0].TargetId);
    }

    // ─── Get ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetNote_OwnNote_ReturnsNote()
    {
        var created = await _service.CreateNoteAsync(new CreateNoteDto { Title = "Mine" }, _caller);

        var result = await _service.GetNoteAsync(created.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Mine", result.Title);
    }

    [TestMethod]
    public async Task GetNote_OtherUser_ReturnsNull()
    {
        var created = await _service.CreateNoteAsync(new CreateNoteDto { Title = "Private" }, _caller);

        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        var result = await _service.GetNoteAsync(created.Id, otherCaller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetNote_NonExistent_ReturnsNull()
    {
        var result = await _service.GetNoteAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    // ─── List ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListNotes_ReturnsOwnNotes()
    {
        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await _service.CreateNoteAsync(new CreateNoteDto { Title = "Mine" }, _caller);
        await _service.CreateNoteAsync(new CreateNoteDto { Title = "Theirs" }, otherCaller);

        var results = await _service.ListNotesAsync(_caller);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Mine", results[0].Title);
    }

    [TestMethod]
    public async Task ListNotes_FilterByFolder()
    {
        var folder = new Models.NoteFolder { OwnerId = _caller.UserId, Name = "Work" };
        _db.NoteFolders.Add(folder);
        await _db.SaveChangesAsync();

        await _service.CreateNoteAsync(new CreateNoteDto { Title = "In Folder", FolderId = folder.Id }, _caller);
        await _service.CreateNoteAsync(new CreateNoteDto { Title = "No Folder" }, _caller);

        var results = await _service.ListNotesAsync(_caller, folderId: folder.Id);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("In Folder", results[0].Title);
    }

    [TestMethod]
    public async Task ListNotes_PinnedFirst()
    {
        await _service.CreateNoteAsync(new CreateNoteDto { Title = "Normal" }, _caller);
        var pinned = await _service.CreateNoteAsync(new CreateNoteDto { Title = "Pinned" }, _caller);
        await _service.UpdateNoteAsync(pinned.Id, new UpdateNoteDto { IsPinned = true }, _caller);

        var results = await _service.ListNotesAsync(_caller);

        Assert.AreEqual("Pinned", results[0].Title);
    }

    // ─── Update ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateNote_ChangesTitle()
    {
        var created = await _service.CreateNoteAsync(new CreateNoteDto { Title = "Old" }, _caller);

        var result = await _service.UpdateNoteAsync(created.Id, new UpdateNoteDto { Title = "New" }, _caller);

        Assert.AreEqual("New", result.Title);
        Assert.AreEqual(2, result.Version);
    }

    [TestMethod]
    public async Task UpdateNote_SavesVersionHistory()
    {
        var created = await _service.CreateNoteAsync(
            new CreateNoteDto { Title = "V1", Content = "content v1" }, _caller);

        await _service.UpdateNoteAsync(created.Id,
            new UpdateNoteDto { Title = "V2", Content = "content v2" }, _caller);

        var versions = await _service.GetVersionHistoryAsync(created.Id, _caller);

        Assert.AreEqual(1, versions.Count);
        Assert.AreEqual("V1", versions[0].Title);
        Assert.AreEqual("content v1", versions[0].Content);
    }

    [TestMethod]
    public async Task UpdateNote_VersionConflict_Throws()
    {
        var created = await _service.CreateNoteAsync(new CreateNoteDto { Title = "Original" }, _caller);

        var ex = await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.UpdateNoteAsync(created.Id,
                new UpdateNoteDto { Title = "Change", ExpectedVersion = 999 }, _caller));

        Assert.IsTrue(ex.Message.Contains("Version", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task UpdateNote_OtherUser_Throws()
    {
        var created = await _service.CreateNoteAsync(new CreateNoteDto { Title = "Private" }, _caller);

        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.UpdateNoteAsync(created.Id, new UpdateNoteDto { Title = "Hacked" }, otherCaller));
    }

    [TestMethod]
    public async Task UpdateNote_ReplacesTags()
    {
        var created = await _service.CreateNoteAsync(
            new CreateNoteDto { Title = "Tagged", Tags = ["old1", "old2"] }, _caller);

        var result = await _service.UpdateNoteAsync(created.Id,
            new UpdateNoteDto { Tags = ["new1", "new2", "new3"] }, _caller);

        Assert.AreEqual(3, result.Tags.Count);
        Assert.IsTrue(result.Tags.Contains("new1"));
        Assert.IsFalse(result.Tags.Contains("old1"));
    }

    [TestMethod]
    public async Task UpdateNote_PublishesEvent()
    {
        var created = await _service.CreateNoteAsync(new CreateNoteDto { Title = "Evt" }, _caller);

        await _service.UpdateNoteAsync(created.Id, new UpdateNoteDto { Title = "Updated" }, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<NoteUpdatedEvent>(e => e.NoteId == created.Id && e.NewVersion == 2),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteNote_SoftDeletes()
    {
        var created = await _service.CreateNoteAsync(new CreateNoteDto { Title = "Doomed" }, _caller);

        await _service.DeleteNoteAsync(created.Id, _caller);

        var result = await _service.GetNoteAsync(created.Id, _caller);
        Assert.IsNull(result); // Soft-deleted, filtered out
    }

    [TestMethod]
    public async Task DeleteNote_PublishesEvent()
    {
        var created = await _service.CreateNoteAsync(new CreateNoteDto { Title = "Deleted" }, _caller);

        await _service.DeleteNoteAsync(created.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<NoteDeletedEvent>(e => e.NoteId == created.Id && !e.IsPermanent),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteNote_OtherUser_Throws()
    {
        var created = await _service.CreateNoteAsync(new CreateNoteDto { Title = "Mine" }, _caller);

        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.DeleteNoteAsync(created.Id, otherCaller));
    }

    // ─── Search ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task SearchNotes_ByTitle()
    {
        await _service.CreateNoteAsync(new CreateNoteDto { Title = "Meeting notes Q4" }, _caller);
        await _service.CreateNoteAsync(new CreateNoteDto { Title = "Shopping list" }, _caller);

        var results = await _service.SearchNotesAsync(_caller, "Meeting");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Meeting notes Q4", results[0].Title);
    }

    [TestMethod]
    public async Task SearchNotes_ByContent()
    {
        await _service.CreateNoteAsync(
            new CreateNoteDto { Title = "Note A", Content = "alpha beta gamma" }, _caller);
        await _service.CreateNoteAsync(
            new CreateNoteDto { Title = "Note B", Content = "delta epsilon" }, _caller);

        var results = await _service.SearchNotesAsync(_caller, "beta");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Note A", results[0].Title);
    }

    [TestMethod]
    public async Task SearchNotes_OtherUserNotVisible()
    {
        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        await _service.CreateNoteAsync(new CreateNoteDto { Title = "Private Search Target" }, otherCaller);

        var results = await _service.SearchNotesAsync(_caller, "Search Target");

        Assert.AreEqual(0, results.Count);
    }

    // ─── Version History ──────────────────────────────────────────────

    [TestMethod]
    public async Task GetVersionHistory_ReturnsVersions()
    {
        var created = await _service.CreateNoteAsync(
            new CreateNoteDto { Title = "V1", Content = "first" }, _caller);
        await _service.UpdateNoteAsync(created.Id,
            new UpdateNoteDto { Title = "V2", Content = "second" }, _caller);
        await _service.UpdateNoteAsync(created.Id,
            new UpdateNoteDto { Title = "V3", Content = "third" }, _caller);

        var versions = await _service.GetVersionHistoryAsync(created.Id, _caller);

        Assert.AreEqual(2, versions.Count);
        // Most recent version first
        Assert.AreEqual("V2", versions[0].Title);
        Assert.AreEqual("V1", versions[1].Title);
    }

    [TestMethod]
    public async Task RestoreVersion_RestoresContent()
    {
        var created = await _service.CreateNoteAsync(
            new CreateNoteDto { Title = "Original", Content = "original content" }, _caller);
        await _service.UpdateNoteAsync(created.Id,
            new UpdateNoteDto { Title = "Changed", Content = "new content" }, _caller);

        var versions = await _service.GetVersionHistoryAsync(created.Id, _caller);
        Assert.AreEqual(1, versions.Count);

        var restored = await _service.RestoreVersionAsync(created.Id, versions[0].Id, _caller);

        Assert.AreEqual("Original", restored.Title);
        Assert.AreEqual("original content", restored.Content);
        Assert.AreEqual(3, restored.Version);
    }

    [TestMethod]
    public async Task RestoreVersion_InvalidVersionId_Throws()
    {
        var created = await _service.CreateNoteAsync(new CreateNoteDto { Title = "V1" }, _caller);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.RestoreVersionAsync(created.Id, Guid.NewGuid(), _caller));
    }
}
