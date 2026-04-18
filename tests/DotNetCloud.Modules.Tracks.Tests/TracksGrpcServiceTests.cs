using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Protos;
using DotNetCloud.Modules.Tracks.Host.Services;
using DotNetCloud.Modules.Tracks.Services;
using Grpc.Core;
using Grpc.Core.Testing;
using Microsoft.Extensions.Logging;
using Moq;

using GrpcTracksService = DotNetCloud.Modules.Tracks.Host.Services.TracksGrpcService;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Unit tests for <see cref="GrpcTracksService"/>.
/// </summary>
[TestClass]
public class TracksGrpcServiceTests
{
    private TracksDbContext _db = null!;
    private GrpcTracksService _grpcService = null!;
    private readonly Guid _userId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        var eventBus = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, new Mock<ILogger<ActivityService>>().Object);
        var teamService = new TeamService(_db, eventBus.Object, new Mock<ILogger<TeamService>>().Object);
        var boardService = new BoardService(_db, eventBus.Object, activityService, teamService, new Mock<ILogger<BoardService>>().Object);
        var swimlaneService = new SwimlaneService(_db, boardService, activityService, new Mock<ILogger<SwimlaneService>>().Object);
        var cardService = new CardService(_db, boardService, activityService, eventBus.Object, new Mock<ILogger<CardService>>().Object);
        var pokerService = new PokerService(_db, boardService, activityService, new NullTracksRealtimeService(), new Mock<ILogger<PokerService>>().Object);
        var sprintPlanningService = new SprintPlanningService(_db, boardService, activityService, new Mock<ILogger<SprintPlanningService>>().Object);
        var reviewSessionService = new ReviewSessionService(_db, boardService, pokerService, new NullTracksRealtimeService(), new Mock<ILogger<ReviewSessionService>>().Object);

        _grpcService = new GrpcTracksService(
            boardService,
            swimlaneService,
            cardService,
            pokerService,
            sprintPlanningService,
            reviewSessionService,
            new Mock<ILogger<GrpcTracksService>>().Object);
    }

    [TestMethod]
    public async Task CreateBoard_ReturnsSuccess()
    {
        var request = new CreateBoardRequest
        {
            UserId = _userId.ToString(),
            Title = "Test Board",
            Description = "Test",
            Color = "#FF0000"
        };
        var ctx = TestServerCallContext.Create(
            method: "CreateBoard",
            host: "localhost",
            deadline: DateTime.UtcNow.AddMinutes(1),
            requestHeaders: new Metadata(),
            cancellationToken: CancellationToken.None,
            peer: "127.0.0.1",
            authContext: null,
            contextPropagationToken: null,
            writeHeadersFunc: _ => Task.CompletedTask,
            writeOptionsGetter: () => new WriteOptions(),
            writeOptionsSetter: _ => { });

        var response = await _grpcService.CreateBoard(request, ctx);
        Assert.IsTrue(response.Success);
        Assert.AreEqual("Test Board", response.Board.Title);
    }

    [TestMethod]
    public async Task GetBoard_ReturnsFailure_WhenNotFound()
    {
        var request = new GetBoardRequest
        {
            BoardId = Guid.NewGuid().ToString(),
            UserId = _userId.ToString()
        };
        var ctx = CreateContext();

        var response = await _grpcService.GetBoard(request, ctx);
        Assert.IsFalse(response.Success);
        Assert.AreEqual("Board not found.", response.ErrorMessage);
    }

    [TestMethod]
    public async Task ListBoards_ReturnsEmptyList()
    {
        var request = new ListBoardsRequest { UserId = _userId.ToString() };
        var ctx = CreateContext();

        var response = await _grpcService.ListBoards(request, ctx);
        Assert.IsTrue(response.Success);
        Assert.AreEqual(0, response.Boards.Count);
    }

    [TestMethod]
    public async Task CreateBoard_ThenGetBoard_ReturnsBoard()
    {
        var ctx = CreateContext();

        var createReq = new CreateBoardRequest
        {
            UserId = _userId.ToString(),
            Title = "My Board"
        };
        var createResponse = await _grpcService.CreateBoard(createReq, ctx);
        Assert.IsTrue(createResponse.Success);

        var getReq = new GetBoardRequest
        {
            BoardId = createResponse.Board.Id,
            UserId = _userId.ToString()
        };
        var getResponse = await _grpcService.GetBoard(getReq, ctx);
        Assert.IsTrue(getResponse.Success);
        Assert.AreEqual("My Board", getResponse.Board.Title);
    }

    [TestMethod]
    public async Task CreateSwimlane_ReturnsSuccess()
    {
        var ctx = CreateContext();
        var board = await _grpcService.CreateBoard(
            new CreateBoardRequest { UserId = _userId.ToString(), Title = "Board" }, ctx);

        var request = new CreateSwimlaneRequest
        {
            BoardId = board.Board.Id,
            UserId = _userId.ToString(),
            Title = "To Do"
        };
        var response = await _grpcService.CreateSwimlane(request, ctx);
        Assert.IsTrue(response.Success);
        Assert.AreEqual("To Do", response.Swimlane.Title);
    }

    [TestMethod]
    public async Task CreateCard_ReturnsSuccess()
    {
        var ctx = CreateContext();
        var board = await _grpcService.CreateBoard(
            new CreateBoardRequest { UserId = _userId.ToString(), Title = "Board" }, ctx);
        var swimlane = await _grpcService.CreateSwimlane(
            new CreateSwimlaneRequest { BoardId = board.Board.Id, UserId = _userId.ToString(), Title = "Todo" }, ctx);

        var request = new CreateCardRequest
        {
            SwimlaneId = swimlane.Swimlane.Id,
            UserId = _userId.ToString(),
            Title = "My Card",
            Priority = "Medium"
        };
        var response = await _grpcService.CreateCard(request, ctx);
        Assert.IsTrue(response.Success);
        Assert.AreEqual("My Card", response.Card.Title);
    }

    [TestMethod]
    public async Task GetCard_ReturnsFailure_WhenNotFound()
    {
        var request = new GetCardRequest
        {
            CardId = Guid.NewGuid().ToString(),
            UserId = _userId.ToString()
        };
        var response = await _grpcService.GetCard(request, CreateContext());
        Assert.IsFalse(response.Success);
    }

    [TestMethod]
    public async Task MoveCard_ReturnsFailure_WhenNotFound()
    {
        var request = new MoveCardRequest
        {
            CardId = Guid.NewGuid().ToString(),
            UserId = _userId.ToString(),
            TargetSwimlaneId = Guid.NewGuid().ToString(),
            Position = 1000
        };
        var response = await _grpcService.MoveCard(request, CreateContext());
        Assert.IsFalse(response.Success);
    }

    [TestMethod]
    public async Task StartPokerSession_InvalidCard_ReturnsFalse()
    {
        // Using a random card ID that doesn't exist in the database
        var request = new StartPokerSessionRequest
        {
            CardId = Guid.NewGuid().ToString(),
            UserId = _userId.ToString(),
            Scale = "Fibonacci"
        };
        var response = await _grpcService.StartPokerSession(request, CreateContext());
        // Poker is implemented — it returns a domain error (card not found / not a board member)
        Assert.IsFalse(response.Success);
    }

    private static ServerCallContext CreateContext()
    {
        return TestServerCallContext.Create(
            method: "test",
            host: "localhost",
            deadline: DateTime.UtcNow.AddMinutes(1),
            requestHeaders: new Metadata(),
            cancellationToken: CancellationToken.None,
            peer: "127.0.0.1",
            authContext: null,
            contextPropagationToken: null,
            writeHeadersFunc: _ => Task.CompletedTask,
            writeOptionsGetter: () => new WriteOptions(),
            writeOptionsSetter: _ => { });
    }
}
