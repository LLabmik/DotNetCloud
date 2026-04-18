using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Controllers;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Unit tests for <see cref="ReviewSessionController"/> covering all 8 endpoints (Phase B).
/// </summary>
[TestClass]
public class ReviewSessionControllerTests
{
    private TracksDbContext _db = null!;
    private ReviewSessionController _controller = null!;
    private BoardService _boardService = null!;
    private ReviewSessionService _reviewSessionService = null!;
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();
    private Board _board = null!;
    private BoardSwimlane _swimlane = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        var eventBus = new Mock<IEventBus>();
        var realtimeMock = new Mock<ITracksRealtimeService>();
        var activityService = new ActivityService(_db, new Mock<ILogger<ActivityService>>().Object);
        var teamService = new TeamService(_db, eventBus.Object, new Mock<ILogger<TeamService>>().Object);
        _boardService = new BoardService(_db, eventBus.Object, activityService, teamService, new Mock<ILogger<BoardService>>().Object);
        var pokerService = new PokerService(_db, _boardService, activityService, realtimeMock.Object, new Mock<ILogger<PokerService>>().Object);
        _reviewSessionService = new ReviewSessionService(_db, _boardService, pokerService, realtimeMock.Object, new Mock<ILogger<ReviewSessionService>>().Object);

        _controller = new ReviewSessionController(_reviewSessionService, new Mock<ILogger<ReviewSessionController>>().Object);
        BoardsControllerTests.SetupControllerContext(_controller, _adminUserId);

        // Seed Team-mode board
        _board = await TestHelpers.SeedBoardAsync(_db, _adminUserId);
        _board.Mode = BoardMode.Team;
        _db.Update(_board);
        await TestHelpers.AddMemberAsync(_db, _board.Id, _memberUserId, BoardMemberRole.Member);
        await _db.SaveChangesAsync();

        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── StartSession ─────────────────────────────────────────────────

    [TestMethod]
    public async Task StartSession_ValidBoard_ReturnsCreated()
    {
        var result = await _controller.StartSessionAsync(_board.Id);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task StartSession_NonExistentBoard_ReturnsNotFound()
    {
        var result = await _controller.StartSessionAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task StartSession_DuplicateActive_ReturnsConflict()
    {
        await _controller.StartSessionAsync(_board.Id);
        var result = await _controller.StartSessionAsync(_board.Id);
        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    // ─── GetActiveSessionForBoard ─────────────────────────────────────

    [TestMethod]
    public async Task GetActiveSession_WithSession_ReturnsOk()
    {
        await _controller.StartSessionAsync(_board.Id);
        var result = await _controller.GetActiveSessionForBoardAsync(_board.Id);
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetActiveSession_NoSession_ReturnsNotFound()
    {
        var result = await _controller.GetActiveSessionForBoardAsync(_board.Id);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetActiveSession_NonExistentBoard_ReturnsNotFound()
    {
        var result = await _controller.GetActiveSessionForBoardAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ─── GetSession ───────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSession_Exists_ReturnsOk()
    {
        var created = await _controller.StartSessionAsync(_board.Id) as CreatedResult;
        Assert.IsNotNull(created?.Value);

        // Extract session ID from the envelope
        var sessionId = ExtractSessionId(created);
        var result = await _controller.GetSessionAsync(sessionId);
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetSession_NonExistent_ReturnsNotFound()
    {
        var result = await _controller.GetSessionAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ─── JoinSession ──────────────────────────────────────────────────

    [TestMethod]
    public async Task JoinSession_ValidSession_ReturnsOk()
    {
        var created = await _controller.StartSessionAsync(_board.Id) as CreatedResult;
        var sessionId = ExtractSessionId(created!);

        // Switch to member context
        BoardsControllerTests.SetupControllerContext(_controller, _memberUserId);
        var result = await _controller.JoinSessionAsync(sessionId);
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task JoinSession_NonExistent_ReturnsNotFound()
    {
        var result = await _controller.JoinSessionAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ─── LeaveSession ─────────────────────────────────────────────────

    [TestMethod]
    public async Task LeaveSession_ValidSession_ReturnsOk()
    {
        var created = await _controller.StartSessionAsync(_board.Id) as CreatedResult;
        var sessionId = ExtractSessionId(created!);

        var result = await _controller.LeaveSessionAsync(sessionId);
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    // ─── SetCurrentCard ───────────────────────────────────────────────

    [TestMethod]
    public async Task SetCurrentCard_AsHost_ReturnsOk()
    {
        var created = await _controller.StartSessionAsync(_board.Id) as CreatedResult;
        var sessionId = ExtractSessionId(created!);
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _adminUserId);

        var dto = new SetReviewCurrentCardDto { CardId = card.Id };
        var result = await _controller.SetCurrentCardAsync(sessionId, dto);
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task SetCurrentCard_NonExistentSession_ReturnsNotFound()
    {
        var dto = new SetReviewCurrentCardDto { CardId = Guid.NewGuid() };
        var result = await _controller.SetCurrentCardAsync(Guid.NewGuid(), dto);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task SetCurrentCard_AsNonHost_Returns403()
    {
        var created = await _controller.StartSessionAsync(_board.Id) as CreatedResult;
        var sessionId = ExtractSessionId(created!);
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _adminUserId);

        // Switch to member context (not the host)
        BoardsControllerTests.SetupControllerContext(_controller, _memberUserId);
        // Member must join first
        await _reviewSessionService.JoinSessionAsync(sessionId, TestHelpers.CreateCaller(_memberUserId));

        var dto = new SetReviewCurrentCardDto { CardId = card.Id };
        var result = await _controller.SetCurrentCardAsync(sessionId, dto);

        // StatusCodeResult for 403
        Assert.IsTrue(result is ObjectResult { StatusCode: 403 });
    }

    // ─── StartPoker ───────────────────────────────────────────────────

    [TestMethod]
    public async Task StartPoker_WithCurrentCard_ReturnsCreated()
    {
        var created = await _controller.StartSessionAsync(_board.Id) as CreatedResult;
        var sessionId = ExtractSessionId(created!);
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _adminUserId);

        await _controller.SetCurrentCardAsync(sessionId, new SetReviewCurrentCardDto { CardId = card.Id });

        var pokerDto = new StartReviewPokerDto { Scale = PokerScale.Fibonacci };
        var result = await _controller.StartPokerAsync(sessionId, pokerDto);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task StartPoker_NonExistentSession_ReturnsNotFound()
    {
        var dto = new StartReviewPokerDto { Scale = PokerScale.Fibonacci };
        var result = await _controller.StartPokerAsync(Guid.NewGuid(), dto);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ─── EndSession ───────────────────────────────────────────────────

    [TestMethod]
    public async Task EndSession_AsHost_ReturnsOk()
    {
        var created = await _controller.StartSessionAsync(_board.Id) as CreatedResult;
        var sessionId = ExtractSessionId(created!);

        var result = await _controller.EndSessionAsync(sessionId);
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task EndSession_NonExistentSession_ReturnsNotFound()
    {
        var result = await _controller.EndSessionAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task EndSession_AsNonHost_Returns403OrBadRequest()
    {
        var created = await _controller.StartSessionAsync(_board.Id) as CreatedResult;
        var sessionId = ExtractSessionId(created!);

        // Switch to member context
        BoardsControllerTests.SetupControllerContext(_controller, _memberUserId);

        var result = await _controller.EndSessionAsync(sessionId);
        // Should be 403 (ReviewSessionNotHost) or BadRequest
        Assert.IsTrue(result is ObjectResult { StatusCode: 403 } or BadRequestObjectResult);
    }

    // ─── Helper ──────────────────────────────────────────────────────

    private static Guid ExtractSessionId(CreatedResult created)
    {
        // The envelope wraps data; extract the session ID from the location URL
        var location = created.Location;
        if (location is not null)
        {
            var lastSlash = location.LastIndexOf('/');
            if (lastSlash >= 0 && Guid.TryParse(location[(lastSlash + 1)..], out var id))
                return id;
        }

        // Fallback: try to extract from envelope value via reflection
        var envelope = created.Value;
        if (envelope is null) return Guid.Empty;

        var dataProp = envelope.GetType().GetProperty("data") ?? envelope.GetType().GetProperty("Data");
        var data = dataProp?.GetValue(envelope);
        if (data is null) return Guid.Empty;

        var idProp = data.GetType().GetProperty("Id");
        return idProp?.GetValue(data) is Guid guid ? guid : Guid.Empty;
    }
}
