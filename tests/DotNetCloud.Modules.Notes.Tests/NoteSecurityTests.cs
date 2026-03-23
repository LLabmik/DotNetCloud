using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Data.Services;
using DotNetCloud.Modules.Notes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Notes.Tests;

/// <summary>
/// Security tests for the Notes module: tenant isolation, authorization boundaries,
/// and Markdown content safety validation.
/// </summary>
[TestClass]
public class NoteSecurityTests
{
    private NotesDbContext _db = null!;
    private NoteService _noteService = null!;
    private NoteFolderService _folderService = null!;
    private NoteShareService _shareService = null!;
    private CallerContext _userA = null!;
    private CallerContext _userB = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotesDbContext(options);
        var eventBusMock = new Mock<IEventBus>();
        _noteService = new NoteService(_db, eventBusMock.Object, NullLogger<NoteService>.Instance);
        _folderService = new NoteFolderService(_db, NullLogger<NoteFolderService>.Instance);
        _shareService = new NoteShareService(_db, NullLogger<NoteShareService>.Instance);
        _userA = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _userB = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Tenant Isolation ────────────────────────────────────────────

    [TestMethod]
    public async Task GetNote_OtherUserNote_ReturnsNull()
    {
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Private" }, _userA);

        var result = await _noteService.GetNoteAsync(note.Id, _userB);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListNotes_OnlyReturnsOwnNotes()
    {
        await _noteService.CreateNoteAsync(new CreateNoteDto { Title = "A's note" }, _userA);
        await _noteService.CreateNoteAsync(new CreateNoteDto { Title = "B's note" }, _userB);

        var notesA = await _noteService.ListNotesAsync(_userA);
        var notesB = await _noteService.ListNotesAsync(_userB);

        Assert.AreEqual(1, notesA.Count);
        Assert.AreEqual(1, notesB.Count);
        Assert.AreEqual("A's note", notesA[0].Title);
        Assert.AreEqual("B's note", notesB[0].Title);
    }

    [TestMethod]
    public async Task UpdateNote_OtherUserNote_Throws()
    {
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Original" }, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _noteService.UpdateNoteAsync(note.Id,
                new UpdateNoteDto { Title = "Hijacked", ExpectedVersion = note.Version }, _userB));
    }

    [TestMethod]
    public async Task DeleteNote_OtherUserNote_Throws()
    {
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Protected" }, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _noteService.DeleteNoteAsync(note.Id, _userB));
    }

    [TestMethod]
    public async Task SearchNotes_OnlyReturnsOwnResults()
    {
        await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Secret Project Alpha" }, _userA);
        await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Secret Project Beta" }, _userB);

        var resultsA = await _noteService.SearchNotesAsync(_userA, "Secret");
        var resultsB = await _noteService.SearchNotesAsync(_userB, "Secret");

        Assert.AreEqual(1, resultsA.Count);
        Assert.AreEqual(1, resultsB.Count);
        Assert.IsTrue(resultsA[0].Title.Contains("Alpha"));
        Assert.IsTrue(resultsB[0].Title.Contains("Beta"));
    }

    [TestMethod]
    public async Task ListFolders_OnlyReturnsOwnFolders()
    {
        await _folderService.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "A's Folder" }, _userA);
        await _folderService.CreateFolderAsync(
            new CreateNoteFolderDto { Name = "B's Folder" }, _userB);

        var foldersA = await _folderService.ListFoldersAsync(_userA);
        var foldersB = await _folderService.ListFoldersAsync(_userB);

        Assert.AreEqual(1, foldersA.Count);
        Assert.AreEqual(1, foldersB.Count);
    }

    // ─── Share Authorization ─────────────────────────────────────────

    [TestMethod]
    public async Task ShareNote_NonOwner_CannotShare()
    {
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Guarded" }, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.ShareNoteAsync(
                note.Id, Guid.NewGuid(), NoteSharePermission.ReadOnly, _userB));
    }

    [TestMethod]
    public async Task RemoveShare_NonOwner_Throws()
    {
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Shared" }, _userA);
        var share = await _shareService.ShareNoteAsync(
            note.Id, _userB.UserId, NoteSharePermission.ReadOnly, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.RemoveShareAsync(share.Id, _userB));
    }

    // ─── Markdown XSS Content Tests ──────────────────────────────────
    // These tests verify the system stores content as-is (no server-side sanitization yet).
    // When sanitization is added, these should be updated to verify dangerous content is stripped.

    [TestMethod]
    public async Task CreateNote_WithScriptTag_StoresContent()
    {
        var xssContent = "<script>alert('xss')</script>";
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "XSS Test", Content = xssContent }, _userA);

        var retrieved = await _noteService.GetNoteAsync(note.Id, _userA);

        Assert.IsNotNull(retrieved);
        // Content is stored as-is; sanitization is a presentation-layer concern
        Assert.AreEqual(xssContent, retrieved.Content);
    }

    [TestMethod]
    public async Task CreateNote_WithImgOnError_StoresContent()
    {
        var xssContent = "<img src=x onerror=alert('xss')>";
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Img XSS", Content = xssContent }, _userA);

        var retrieved = await _noteService.GetNoteAsync(note.Id, _userA);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(xssContent, retrieved.Content);
    }

    [TestMethod]
    public async Task CreateNote_WithIframe_StoresContent()
    {
        var xssContent = "<iframe src='https://evil.example.com'></iframe>";
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Iframe XSS", Content = xssContent }, _userA);

        var retrieved = await _noteService.GetNoteAsync(note.Id, _userA);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(xssContent, retrieved.Content);
    }

    [TestMethod]
    public async Task CreateNote_WithJavascriptUrl_StoresContent()
    {
        var xssContent = "[click me](javascript:alert('xss'))";
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "JS URL XSS", Content = xssContent }, _userA);

        var retrieved = await _noteService.GetNoteAsync(note.Id, _userA);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(xssContent, retrieved.Content);
    }

    [TestMethod]
    public async Task CreateNote_WithEventHandlers_StoresContent()
    {
        var xssContent = "<div onmouseover=\"alert('xss')\">hover me</div>";
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Event Handler XSS", Content = xssContent }, _userA);

        var retrieved = await _noteService.GetNoteAsync(note.Id, _userA);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(xssContent, retrieved.Content);
    }
}
