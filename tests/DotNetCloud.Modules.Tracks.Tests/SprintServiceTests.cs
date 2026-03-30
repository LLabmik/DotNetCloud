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
public class SprintServiceTests
{
    private TracksDbContext _db;
    private SprintService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;
    private Board _board;
    private BoardSwimlane _swimlane;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        _eventBusMock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, _eventBusMock.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new SprintService(_db, boardService, activityService, _eventBusMock.Object, NullLogger<SprintService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateSprint_ValidDto_ReturnsSprint()
    {
        var dto = new CreateSprintDto
        {
            Title = "Sprint 1",
            Goal = "Ship features",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14)
        };

        var result = await _service.CreateSprintAsync(_board.Id, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Sprint 1", result.Title);
        Assert.AreEqual("Ship features", result.Goal);
        Assert.AreEqual(SprintStatus.Planning, result.Status);
    }

    [TestMethod]
    public async Task CreateSprint_AsMember_Throws()
    {
        var memberCaller = TestHelpers.CreateCaller();
        await TestHelpers.AddMemberAsync(_db, _board.Id, memberCaller.UserId, BoardMemberRole.Member);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "X" }, memberCaller));
    }

    // ─── Get Sprint ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSprint_ExistingSprint_ReturnsSprint()
    {
        var created = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);

        var result = await _service.GetSprintAsync(created.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("S1", result.Title);
    }

    [TestMethod]
    public async Task GetSprint_NonExistent_ReturnsNull()
    {
        var result = await _service.GetSprintAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    // ─── List Sprints ─────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSprints_ReturnsSprintsForBoard()
    {
        await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);
        await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S2" }, _caller);

        var results = await _service.GetSprintsAsync(_board.Id, _caller);

        Assert.AreEqual(2, results.Count);
    }

    // ─── Start Sprint ─────────────────────────────────────────────────

    [TestMethod]
    public async Task StartSprint_FromPlanning_Starts()
    {
        var sprint = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);

        var result = await _service.StartSprintAsync(sprint.Id, _caller);

        Assert.AreEqual(SprintStatus.Active, result.Status);
    }

    [TestMethod]
    public async Task StartSprint_PublishesEvent()
    {
        var sprint = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);

        await _service.StartSprintAsync(sprint.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SprintStartedEvent>(e => e.SprintId == sprint.Id),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task StartSprint_WhenActiveSprintExists_Throws()
    {
        var sprint1 = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);
        await _service.StartSprintAsync(sprint1.Id, _caller);

        var sprint2 = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S2" }, _caller);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.StartSprintAsync(sprint2.Id, _caller));
    }

    [TestMethod]
    public async Task StartSprint_FromCompletedStatus_Throws()
    {
        var sprint = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);
        await _service.StartSprintAsync(sprint.Id, _caller);
        await _service.CompleteSprintAsync(sprint.Id, _caller);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.StartSprintAsync(sprint.Id, _caller));
    }

    // ─── Complete Sprint ──────────────────────────────────────────────

    [TestMethod]
    public async Task CompleteSprint_FromActive_Completes()
    {
        var sprint = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);
        await _service.StartSprintAsync(sprint.Id, _caller);

        var result = await _service.CompleteSprintAsync(sprint.Id, _caller);

        Assert.AreEqual(SprintStatus.Completed, result.Status);
    }

    [TestMethod]
    public async Task CompleteSprint_FromPlanning_Throws()
    {
        var sprint = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.CompleteSprintAsync(sprint.Id, _caller));
    }

    // ─── Delete Sprint ────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteSprint_RemovesSprint()
    {
        var sprint = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);

        await _service.DeleteSprintAsync(sprint.Id, _caller);

        Assert.IsFalse(await _db.Sprints.AnyAsync(s => s.Id == sprint.Id));
    }

    // ─── Sprint Cards ─────────────────────────────────────────────────

    [TestMethod]
    public async Task AddCardToSprint_AddsCard()
    {
        var sprint = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId);

        await _service.AddCardToSprintAsync(sprint.Id, card.Id, _caller);

        Assert.IsTrue(await _db.SprintCards.AnyAsync(sc => sc.SprintId == sprint.Id && sc.CardId == card.Id));
    }

    [TestMethod]
    public async Task AddCardToSprint_AlreadyAdded_IsIdempotent()
    {
        var sprint = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId);

        await _service.AddCardToSprintAsync(sprint.Id, card.Id, _caller);
        await _service.AddCardToSprintAsync(sprint.Id, card.Id, _caller);

        var count = await _db.SprintCards.CountAsync(sc => sc.SprintId == sprint.Id && sc.CardId == card.Id);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task RemoveCardFromSprint_RemovesCard()
    {
        var sprint = await _service.CreateSprintAsync(_board.Id, new CreateSprintDto { Title = "S1" }, _caller);
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId);
        await _service.AddCardToSprintAsync(sprint.Id, card.Id, _caller);

        await _service.RemoveCardFromSprintAsync(sprint.Id, card.Id, _caller);

        Assert.IsFalse(await _db.SprintCards.AnyAsync(sc => sc.SprintId == sprint.Id && sc.CardId == card.Id));
    }
}
