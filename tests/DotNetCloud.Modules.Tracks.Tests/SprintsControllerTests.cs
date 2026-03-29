using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Unit tests for <see cref="SprintsController"/>.
/// </summary>
[TestClass]
public class SprintsControllerTests
{
    private TracksDbContext _db = null!;
    private SprintsController _controller = null!;
    private BoardService _boardService = null!;
    private readonly Guid _userId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        var eventBus = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, new Mock<ILogger<ActivityService>>().Object);
        var teamService = new TeamService(_db, eventBus.Object, new Mock<ILogger<TeamService>>().Object);
        _boardService = new BoardService(_db, eventBus.Object, activityService, teamService, new Mock<ILogger<BoardService>>().Object);
        var sprintService = new SprintService(_db, _boardService, activityService, eventBus.Object, new Mock<ILogger<SprintService>>().Object);
        // Note: SprintService requires eventBus for publishing SprintStartedEvent/SprintCompletedEvent

        _controller = new SprintsController(sprintService, new Mock<ILogger<SprintsController>>().Object);
        BoardsControllerTests.SetupControllerContext(_controller, _userId);
    }

    [TestMethod]
    public async Task ListSprints_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var result = await _controller.ListSprintsAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetSprint_ReturnsNotFound_WhenSprintDoesNotExist()
    {
        var result = await _controller.GetSprintAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateSprint_ReturnsCreated_WhenValid()
    {
        var caller = TestHelpers.CreateCaller(_userId);
        var board = await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "Board" }, caller);
        var dto = new CreateSprintDto { Title = "Sprint 1" };
        var result = await _controller.CreateSprintAsync(board.Id, dto);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task CreateSprint_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var dto = new CreateSprintDto { Title = "Sprint 1" };
        var result = await _controller.CreateSprintAsync(Guid.NewGuid(), dto);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteSprint_ReturnsNotFound_WhenSprintDoesNotExist()
    {
        var result = await _controller.DeleteSprintAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task StartSprint_ReturnsNotFound_WhenSprintDoesNotExist()
    {
        var result = await _controller.StartSprintAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task CompleteSprint_ReturnsNotFound_WhenSprintDoesNotExist()
    {
        var result = await _controller.CompleteSprintAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }
}
