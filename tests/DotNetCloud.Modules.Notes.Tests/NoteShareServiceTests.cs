using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Data.Services;
using DotNetCloud.Modules.Notes.Models;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Notes.Tests;

[TestClass]
public class NoteShareServiceTests
{
    private NotesDbContext _db;
    private NoteShareService _shareService;
    private NoteService _noteService;
    private CallerContext _owner;
    private CallerContext _recipient;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotesDbContext(options);
        var eventBusMock = new Mock<IEventBus>();
        _noteService = new NoteService(_db, eventBusMock.Object, NullLogger<NoteService>.Instance);
        _shareService = new NoteShareService(_db, eventBusMock.Object, NullLogger<NoteShareService>.Instance);
        _owner = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _recipient = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    // ─── Share ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ShareNote_OwnerCanShare()
    {
        var note = await _noteService.CreateNoteAsync(new CreateNoteDto { Title = "Shared" }, _owner);

        var share = await _shareService.ShareNoteAsync(
            note.Id, _recipient.UserId, NoteSharePermission.ReadOnly, _owner);

        Assert.IsNotNull(share);
        Assert.AreEqual(note.Id, share.NoteId);
        Assert.AreEqual(_recipient.UserId, share.SharedWithUserId);
        Assert.AreEqual(NoteSharePermission.ReadOnly, share.Permission);
    }

    [TestMethod]
    public async Task ShareNote_NonOwner_Throws()
    {
        var note = await _noteService.CreateNoteAsync(new CreateNoteDto { Title = "Private" }, _owner);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.ShareNoteAsync(
                note.Id, Guid.NewGuid(), NoteSharePermission.ReadOnly, _recipient));
    }

    [TestMethod]
    public async Task ShareNote_Upsert_UpdatesPermission()
    {
        var note = await _noteService.CreateNoteAsync(new CreateNoteDto { Title = "Upgrade" }, _owner);

        await _shareService.ShareNoteAsync(
            note.Id, _recipient.UserId, NoteSharePermission.ReadOnly, _owner);
        var updated = await _shareService.ShareNoteAsync(
            note.Id, _recipient.UserId, NoteSharePermission.ReadWrite, _owner);

        Assert.AreEqual(NoteSharePermission.ReadWrite, updated.Permission);
    }

    // ─── List ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListShares_ReturnsSharesForNote()
    {
        var note = await _noteService.CreateNoteAsync(new CreateNoteDto { Title = "Multi share" }, _owner);
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        await _shareService.ShareNoteAsync(note.Id, user2, NoteSharePermission.ReadOnly, _owner);
        await _shareService.ShareNoteAsync(note.Id, user3, NoteSharePermission.ReadWrite, _owner);

        var shares = await _shareService.ListSharesAsync(note.Id, _owner);

        Assert.AreEqual(2, shares.Count);
    }

    // ─── Remove ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task RemoveShare_OwnerCanRemove()
    {
        var note = await _noteService.CreateNoteAsync(new CreateNoteDto { Title = "Remove me" }, _owner);
        var share = await _shareService.ShareNoteAsync(
            note.Id, _recipient.UserId, NoteSharePermission.ReadOnly, _owner);

        await _shareService.RemoveShareAsync(share.Id, _owner);

        var shares = await _shareService.ListSharesAsync(note.Id, _owner);
        Assert.AreEqual(0, shares.Count);
    }

    [TestMethod]
    public async Task RemoveShare_NonOwner_Throws()
    {
        var note = await _noteService.CreateNoteAsync(new CreateNoteDto { Title = "Protected" }, _owner);
        var share = await _shareService.ShareNoteAsync(
            note.Id, _recipient.UserId, NoteSharePermission.ReadOnly, _owner);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.RemoveShareAsync(share.Id, _recipient));
    }

    // ─── Visibility ───────────────────────────────────────────────────

    [TestMethod]
    public async Task SharedNote_VisibleToRecipient()
    {
        var note = await _noteService.CreateNoteAsync(
            new CreateNoteDto { Title = "Shared Access" }, _owner);

        await _shareService.ShareNoteAsync(
            note.Id, _recipient.UserId, NoteSharePermission.ReadOnly, _owner);

        // Recipient should be able to get the note via GetNoteAsync (checks shares)
        var result = await _noteService.GetNoteAsync(note.Id, _recipient);
        Assert.IsNotNull(result);
        Assert.AreEqual("Shared Access", result.Title);
    }
}
