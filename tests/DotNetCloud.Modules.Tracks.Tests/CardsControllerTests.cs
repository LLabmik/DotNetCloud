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
/// Unit tests for <see cref="CardsController"/>.
/// </summary>
[TestClass]
public class CardsControllerTests
{
    private TracksDbContext _db = null!;
    private CardsController _controller = null!;
    private BoardService _boardService = null!;
    private ListService _listService = null!;
    private CardService _cardService = null!;
    private readonly Guid _userId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        var eventBus = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, new Mock<ILogger<ActivityService>>().Object);
        _boardService = new BoardService(_db, eventBus.Object, activityService, new Mock<ILogger<BoardService>>().Object);
        _listService = new ListService(_db, _boardService, activityService, new Mock<ILogger<ListService>>().Object);
        _cardService = new CardService(_db, _boardService, activityService, eventBus.Object, new Mock<ILogger<CardService>>().Object);
        var labelService = new LabelService(_db, _boardService, activityService, new Mock<ILogger<LabelService>>().Object);

        _controller = new CardsController(
            _cardService,
            labelService,
            activityService,
            new Mock<ILogger<CardsController>>().Object);
        BoardsControllerTests.SetupControllerContext(_controller, _userId);
    }

    [TestMethod]
    public async Task GetCard_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var result = await _controller.GetCardAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateCard_ReturnsCreated_WhenValid()
    {
        var caller = TestHelpers.CreateCaller(_userId);
        var board = await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "Board" }, caller);
        var list = await _listService.CreateListAsync(board.Id, new CreateBoardListDto { Title = "Todo" }, caller);

        var dto = new CreateCardDto
        {
            Title = "My Card",
            Priority = CardPriority.Medium,
            AssigneeIds = [],
            LabelIds = []
        };

        var result = await _controller.CreateCardAsync(list.Id, dto);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task CreateCard_ReturnsNotFound_WhenListDoesNotExist()
    {
        var dto = new CreateCardDto
        {
            Title = "Card",
            Priority = CardPriority.None,
            AssigneeIds = [],
            LabelIds = []
        };
        var result = await _controller.CreateCardAsync(Guid.NewGuid(), dto);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task MoveCard_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var dto = new MoveCardDto { TargetListId = Guid.NewGuid(), Position = 1000 };
        var result = await _controller.MoveCardAsync(Guid.NewGuid(), dto);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteCard_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var result = await _controller.DeleteCardAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task AssignUser_ReturnsBadRequest_WhenCardDoesNotExist()
    {
        var request = new CardAssignRequest { UserId = Guid.NewGuid() };
        var result = await _controller.AssignUserAsync(Guid.NewGuid(), request);
        // Card not found throws ValidationException; controller maps it
        Assert.IsTrue(result is NotFoundObjectResult || result is BadRequestObjectResult);
    }

    [TestMethod]
    public async Task GetCardActivity_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var result = await _controller.GetCardActivityAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }
}
