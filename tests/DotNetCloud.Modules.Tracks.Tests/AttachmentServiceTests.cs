using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class AttachmentServiceTests
{
    private TracksDbContext _db;
    private AttachmentService _service;
    private CallerContext _caller;
    private Board _board;
    private Card _card;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, new Mock<IEventBus>().Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, new Mock<IEventBus>().Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new AttachmentService(_db, boardService, activityService, NullLogger<AttachmentService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        var list = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        _card = await TestHelpers.SeedCardAsync(_db, list.Id, _caller.UserId);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Add Attachment ───────────────────────────────────────────────

    [TestMethod]
    public async Task AddAttachment_WithFileNodeId_ReturnsAttachment()
    {
        var fileNodeId = Guid.NewGuid();

        var result = await _service.AddAttachmentAsync(_card.Id, "doc.pdf", fileNodeId, null, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("doc.pdf", result.FileName);
        Assert.AreEqual(fileNodeId, result.FileNodeId);
        Assert.IsNull(result.Url);
        Assert.AreEqual(_caller.UserId, result.AddedByUserId);
    }

    [TestMethod]
    public async Task AddAttachment_WithUrl_ReturnsAttachment()
    {
        var result = await _service.AddAttachmentAsync(_card.Id, "link.html", null, "https://example.com", _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("https://example.com", result.Url);
        Assert.IsNull(result.FileNodeId);
    }

    // ─── Get Attachments ──────────────────────────────────────────────

    [TestMethod]
    public async Task GetAttachments_ReturnsAttachments()
    {
        await _service.AddAttachmentAsync(_card.Id, "file1.txt", null, null, _caller);
        await _service.AddAttachmentAsync(_card.Id, "file2.txt", null, null, _caller);

        var results = await _service.GetAttachmentsAsync(_card.Id, _caller);

        Assert.AreEqual(2, results.Count);
    }

    // ─── Remove Attachment ────────────────────────────────────────────

    [TestMethod]
    public async Task RemoveAttachment_RemovesFromDb()
    {
        var attachment = await _service.AddAttachmentAsync(_card.Id, "doomed.txt", null, null, _caller);

        await _service.RemoveAttachmentAsync(attachment.Id, _caller);

        Assert.IsFalse(await _db.CardAttachments.AnyAsync(a => a.Id == attachment.Id));
    }

    [TestMethod]
    public async Task RemoveAttachment_NonExistent_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.RemoveAttachmentAsync(Guid.NewGuid(), _caller));
    }
}
