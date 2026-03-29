using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Unit tests for <see cref="BoardsController"/>.
/// </summary>
[TestClass]
public class BoardsControllerTests
{
    private BoardsController _controller = null!;
    private BoardService _boardService = null!;
    private readonly Guid _userId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        // Create real service instances with in-memory db for mocking constructors
        var db = TestHelpers.CreateDb();
        var eventBusMock = new Mock<DotNetCloud.Core.Events.IEventBus>();
        var loggerBoardMock = new Mock<ILogger<BoardService>>();
        var loggerActivityMock = new Mock<ILogger<ActivityService>>();
        var loggerLabelMock = new Mock<ILogger<LabelService>>();

        var activityService = new ActivityService(db, loggerActivityMock.Object);
        var teamService = new TeamService(db, eventBusMock.Object, new Mock<ILogger<TeamService>>().Object);
        _boardService = new BoardService(db, eventBusMock.Object, activityService, teamService, loggerBoardMock.Object);
        var labelService = new LabelService(db, _boardService, activityService, loggerLabelMock.Object);

        _controller = new BoardsController(
            _boardService,
            activityService,
            labelService,
            teamService,
            new Mock<ILogger<BoardsController>>().Object);

        SetupControllerContext(_controller, _userId);
    }

    [TestMethod]
    public async Task ListBoards_ReturnsOk_WhenNoBoards()
    {
        var result = await _controller.ListBoardsAsync();
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateBoard_ReturnsCreated()
    {
        var dto = new CreateBoardDto { Title = "My Board" };
        var result = await _controller.CreateBoardAsync(dto);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task GetBoard_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var result = await _controller.GetBoardAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetBoard_ReturnsOk_WhenBoardExists()
    {
        var caller = TestHelpers.CreateCaller(_userId);
        var board = await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "Board" }, caller);

        var result = await _controller.GetBoardAsync(board.Id);
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteBoard_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var result = await _controller.DeleteBoardAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task UpdateBoard_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var dto = new UpdateBoardDto { Title = "Updated" };
        var result = await _controller.UpdateBoardAsync(Guid.NewGuid(), dto);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetBoardActivity_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var result = await _controller.GetBoardActivityAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task ListMembers_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var result = await _controller.ListMembersAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateLabel_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var dto = new CreateLabelDto { Title = "Bug", Color = "#FF0000" };
        var result = await _controller.CreateLabelAsync(Guid.NewGuid(), dto);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task ExportBoard_ReturnsNotFound_WhenBoardDoesNotExist()
    {
        var result = await _controller.ExportBoardAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task ImportBoard_ReturnsCreated()
    {
        var dto = new CreateBoardDto { Title = "Imported Board" };
        var result = await _controller.ImportBoardAsync(dto);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    internal static void SetupControllerContext(ControllerBase controller, Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "user")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
