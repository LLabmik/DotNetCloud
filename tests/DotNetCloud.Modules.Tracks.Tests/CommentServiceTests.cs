using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class CommentServiceTests
{
    private TracksDbContext _db;
    private CommentService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;
    private Board _board;
    private BoardSwimlane _swimlane;
    private Card _card;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        _eventBusMock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, _eventBusMock.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new CommentService(_db, boardService, activityService, _eventBusMock.Object, NullLogger<CommentService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        _card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateComment_ValidContent_ReturnsComment()
    {
        var result = await _service.CreateCommentAsync(_card.Id, "Hello world", _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Hello world", result.Content);
        Assert.AreEqual(_caller.UserId, result.UserId);
        Assert.AreEqual(_card.Id, result.CardId);
    }

    [TestMethod]
    public async Task CreateComment_PublishesEvent()
    {
        await _service.CreateCommentAsync(_card.Id, "Event comment", _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<CardCommentAddedEvent>(e => e.CardId == _card.Id),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Get Comments ─────────────────────────────────────────────────

    [TestMethod]
    public async Task GetComments_ReturnsOrderedComments()
    {
        await _service.CreateCommentAsync(_card.Id, "First", _caller);
        await _service.CreateCommentAsync(_card.Id, "Second", _caller);

        var results = await _service.GetCommentsAsync(_card.Id, _caller);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("First", results[0].Content);
        Assert.AreEqual("Second", results[1].Content);
    }

    // ─── Update ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateComment_AsAuthor_Updates()
    {
        var comment = await _service.CreateCommentAsync(_card.Id, "Original", _caller);

        var result = await _service.UpdateCommentAsync(comment.Id, "Updated", _caller);

        Assert.AreEqual("Updated", result.Content);
    }

    [TestMethod]
    public async Task UpdateComment_AsOtherUser_Throws()
    {
        var comment = await _service.CreateCommentAsync(_card.Id, "Original", _caller);
        var otherCaller = TestHelpers.CreateCaller();
        await TestHelpers.AddMemberAsync(_db, _board.Id, otherCaller.UserId, BoardMemberRole.Member);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.UpdateCommentAsync(comment.Id, "Hacked", otherCaller));
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteComment_AsAuthor_SoftDeletes()
    {
        var comment = await _service.CreateCommentAsync(_card.Id, "Doomed", _caller);

        await _service.DeleteCommentAsync(comment.Id, _caller);

        var dbComment = await _db.CardComments.FindAsync(comment.Id);
        Assert.IsTrue(dbComment!.IsDeleted);
    }

    [TestMethod]
    public async Task DeleteComment_AsAdmin_SoftDeletes()
    {
        var authorCaller = TestHelpers.CreateCaller();
        await TestHelpers.AddMemberAsync(_db, _board.Id, authorCaller.UserId, BoardMemberRole.Member);
        var comment = await _service.CreateCommentAsync(_card.Id, "Author comment", authorCaller);

        // Owner (who is Admin+) can delete other's comments
        await _service.DeleteCommentAsync(comment.Id, _caller);

        var dbComment = await _db.CardComments.FindAsync(comment.Id);
        Assert.IsTrue(dbComment!.IsDeleted);
    }
}
