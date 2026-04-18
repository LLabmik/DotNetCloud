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
/// Unit tests for <see cref="SwimlanesController"/>.
/// </summary>
[TestClass]
public class SwimlanesControllerTests
{
    private TracksDbContext _db = null!;
    private SwimlanesController _controller = null!;
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
        var swimlaneService = new SwimlaneService(_db, _boardService, activityService, new Mock<ILogger<SwimlaneService>>().Object);

        _controller = new SwimlanesController(swimlaneService, new Mock<ILogger<SwimlanesController>>().Object);
        BoardsControllerTests.SetupControllerContext(_controller, _userId);
    }

    [TestMethod]
    public async Task ListLists_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var result = await _controller.ListSwimlanesAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateList_ReturnsCreated_WhenValid()
    {
        var caller = TestHelpers.CreateCaller(_userId);
        var board = await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "Board" }, caller);
        var dto = new CreateBoardSwimlaneDto { Title = "Todo" };
        var result = await _controller.CreateSwimlaneAsync(board.Id, dto);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task CreateList_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var dto = new CreateBoardSwimlaneDto { Title = "Todo" };
        var result = await _controller.CreateSwimlaneAsync(Guid.NewGuid(), dto);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteList_ReturnsNotFound_WhenListDoesNotExist()
    {
        var result = await _controller.DeleteSwimlaneAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task UpdateList_ReturnsNotFound_WhenListDoesNotExist()
    {
        var dto = new UpdateBoardSwimlaneDto { Title = "Updated" };
        var result = await _controller.UpdateSwimlaneAsync(Guid.NewGuid(), Guid.NewGuid(), dto);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }
}
