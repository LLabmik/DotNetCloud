using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class SprintReportServiceTests
{
    private TracksDbContext _db = null!;
    private SprintReportService _service = null!;
    private SprintService _sprintService = null!;
    private CallerContext _caller;
    private Board _board = null!;
    private BoardSwimlane _swimlane = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        var mock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, mock.Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, mock.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new SprintReportService(_db, boardService, NullLogger<SprintReportService>.Instance);
        _sprintService = new SprintService(_db, boardService, activityService, mock.Object, NullLogger<SprintService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── GetSprintReport ──────────────────────────────────────────────

    [TestMethod]
    public async Task GetSprintReport_ValidSprint_ReturnsReport()
    {
        var sprint = await _sprintService.CreateSprintAsync(_board.Id, new CreateSprintDto
        {
            Title = "Sprint 1",
            StartDate = DateTime.UtcNow.AddDays(-14),
            EndDate = DateTime.UtcNow.AddDays(0)
        }, _caller);

        var result = await _service.GetSprintReportAsync(sprint.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(sprint.Id, result.SprintId);
        Assert.AreEqual("Sprint 1", result.Title);
    }

    [TestMethod]
    public async Task GetSprintReport_SprintNotFound_Throws()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.GetSprintReportAsync(Guid.NewGuid(), _caller));
    }

    [TestMethod]
    public async Task GetSprintReport_WithBurndownData_PopulatesPoints()
    {
        var sprint = await _sprintService.CreateSprintAsync(_board.Id, new CreateSprintDto
        {
            Title = "Sprint 2",
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(9)
        }, _caller);

        // Add a card to the sprint via SprintCard junction
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "Feature");
        card.StoryPoints = 5;
        _db.SprintCards.Add(new SprintCard { SprintId = sprint.Id, CardId = card.Id });
        await _db.SaveChangesAsync();

        var result = await _service.GetSprintReportAsync(sprint.Id, _caller);

        Assert.IsNotNull(result.BurndownData);
        Assert.AreEqual(5, result.TotalPoints);
    }

    // ─── GetBoardVelocity ─────────────────────────────────────────────

    [TestMethod]
    public async Task GetBoardVelocity_NoCompletedSprints_ReturnsEmpty()
    {
        var result = await _service.GetBoardVelocityAsync(_board.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetBoardVelocity_NonMember_Throws()
    {
        var outsider = TestHelpers.CreateCaller();

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.GetBoardVelocityAsync(_board.Id, outsider));
    }
}
