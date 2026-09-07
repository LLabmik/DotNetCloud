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
/// Tests for <see cref="NoteService.GetRecentNotesAsync"/>.
/// </summary>
[TestClass]
public class NoteServiceGetRecentNotesTests
{
    private NotesDbContext _db = null!;
    private NoteService _service = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new NotesDbContext(options);
        _service = new NoteService(
            _db,
            new Mock<IEventBus>().Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            NullLogger<NoteService>.Instance);
        _caller = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task<Note> SeedNoteAsync(Guid ownerId, string title, DateTime updatedAt, bool isDeleted = false)
    {
        var note = new Note
        {
            OwnerId = ownerId,
            Title = title,
            Content = "Body",
            UpdatedAt = updatedAt,
            IsDeleted = isDeleted
        };
        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        return note;
    }

    private async Task SeedShareAsync(Guid noteId, Guid sharedByUserId, Guid sharedWithUserId)
    {
        _db.NoteShares.Add(new NoteShare
        {
            NoteId = noteId,
            CreatedByUserId = sharedByUserId,
            SharedWithUserId = sharedWithUserId,
            Permission = NoteSharePermission.ReadOnly
        });
        await _db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task GetRecentNotes_OrdersByUpdatedAtDescending_ReturnsNewestFirst()
    {
        var now = DateTime.UtcNow;

        await SeedNoteAsync(_caller.UserId, "Oldest", now.AddDays(-3));
        await SeedNoteAsync(_caller.UserId, "Middle", now.AddDays(-2));
        await SeedNoteAsync(_caller.UserId, "Newest", now.AddDays(-1));

        var result = await _service.GetRecentNotesAsync(_caller);

        CollectionAssert.AreEqual(
            new[] { "Newest", "Middle", "Oldest" },
            result.Select(n => n.Title).ToArray());
    }

    [TestMethod]
    public async Task GetRecentNotes_RespectsCount_ReturnsOnlyRequestedNumberOfNewest()
    {
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
            await SeedNoteAsync(_caller.UserId, $"Note{i}", now.AddMinutes(i));

        var result = await _service.GetRecentNotesAsync(_caller, count: 2);

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual(
            new[] { "Note4", "Note3" },
            result.Select(n => n.Title).ToArray());
    }

    [TestMethod]
    public async Task GetRecentNotes_SharedWithCaller_IncludesSharedNotes()
    {
        var otherOwner = Guid.CreateVersion7();
        var sharedNote = await SeedNoteAsync(otherOwner, "Shared With Me", DateTime.UtcNow.AddDays(-1));
        await SeedShareAsync(sharedNote.Id, otherOwner, _caller.UserId);
        await SeedNoteAsync(_caller.UserId, "My Own", DateTime.UtcNow);

        var result = await _service.GetRecentNotesAsync(_caller);

        CollectionAssert.AreEquivalent(
            new[] { "My Own", "Shared With Me" },
            result.Select(n => n.Title).ToArray());
    }

    [TestMethod]
    public async Task GetRecentNotes_SoftDeletedNote_IsExcluded()
    {
        await SeedNoteAsync(_caller.UserId, "Live", DateTime.UtcNow);
        await SeedNoteAsync(_caller.UserId, "Deleted", DateTime.UtcNow.AddHours(1), isDeleted: true);

        var result = await _service.GetRecentNotesAsync(_caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Live", result[0].Title);
    }

    [TestMethod]
    public async Task GetRecentNotes_NoNotes_ReturnsEmptyList()
    {
        var result = await _service.GetRecentNotesAsync(_caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }
}
