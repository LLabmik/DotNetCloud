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
/// Unit tests for <see cref="CommentsController"/>, <see cref="ChecklistsController"/>,
/// <see cref="AttachmentsController"/>, <see cref="DependenciesController"/>,
/// and <see cref="TimeEntriesController"/>.
/// </summary>
[TestClass]
public class SubresourceControllerTests
{
    private TracksDbContext _db = null!;
    private CommentsController _commentsController = null!;
    private ChecklistsController _checklistsController = null!;
    private AttachmentsController _attachmentsController = null!;
    private DependenciesController _dependenciesController = null!;
    private TimeEntriesController _timeEntriesController = null!;
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
        var teamService = new TeamService(_db, eventBus.Object, new Mock<ILogger<TeamService>>().Object);
        _boardService = new BoardService(_db, eventBus.Object, activityService, teamService, new Mock<ILogger<BoardService>>().Object);
        _listService = new ListService(_db, _boardService, activityService, new Mock<ILogger<ListService>>().Object);
        _cardService = new CardService(_db, _boardService, activityService, eventBus.Object, new Mock<ILogger<CardService>>().Object);
        var commentService = new CommentService(_db, _boardService, activityService, eventBus.Object, new Mock<ILogger<CommentService>>().Object);
        var checklistService = new ChecklistService(_db, _boardService, activityService, new Mock<ILogger<ChecklistService>>().Object);
        var attachmentService = new AttachmentService(_db, _boardService, activityService, new Mock<ILogger<AttachmentService>>().Object);
        var dependencyService = new DependencyService(_db, _boardService, activityService, new Mock<ILogger<DependencyService>>().Object);
        var timeService = new TimeTrackingService(_db, _boardService, activityService, new Mock<ILogger<TimeTrackingService>>().Object);

        _commentsController = new CommentsController(commentService, new Mock<ILogger<CommentsController>>().Object);
        _checklistsController = new ChecklistsController(checklistService, new Mock<ILogger<ChecklistsController>>().Object);
        _attachmentsController = new AttachmentsController(attachmentService, new Mock<ILogger<AttachmentsController>>().Object);
        _dependenciesController = new DependenciesController(dependencyService, new Mock<ILogger<DependenciesController>>().Object);
        _timeEntriesController = new TimeEntriesController(timeService, new Mock<ILogger<TimeEntriesController>>().Object);

        BoardsControllerTests.SetupControllerContext(_commentsController, _userId);
        BoardsControllerTests.SetupControllerContext(_checklistsController, _userId);
        BoardsControllerTests.SetupControllerContext(_attachmentsController, _userId);
        BoardsControllerTests.SetupControllerContext(_dependenciesController, _userId);
        BoardsControllerTests.SetupControllerContext(_timeEntriesController, _userId);
    }

    // ─── Comments ───────────────────────────────────────────────────

    [TestMethod]
    public async Task ListComments_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var result = await _commentsController.ListCommentsAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateComment_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var request = new CreateCommentRequest { Content = "Hello" };
        var result = await _commentsController.CreateCommentAsync(Guid.NewGuid(), request);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteComment_ReturnsNotFound_WhenCommentDoesNotExist()
    {
        var result = await _commentsController.DeleteCommentAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateComment_ReturnsCreated_WhenValid()
    {
        var (_, cardId) = await CreateBoardListCard();
        var request = new CreateCommentRequest { Content = "Great work!" };
        var result = await _commentsController.CreateCommentAsync(cardId, request);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    // ─── Checklists ─────────────────────────────────────────────────

    [TestMethod]
    public async Task ListChecklists_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var result = await _checklistsController.ListChecklistsAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateChecklist_ReturnsCreated_WhenValid()
    {
        var (_, cardId) = await CreateBoardListCard();
        var request = new CreateChecklistRequest { Title = "TODO" };
        var result = await _checklistsController.CreateChecklistAsync(cardId, request);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task DeleteChecklist_ReturnsNotFound_WhenDoesNotExist()
    {
        var result = await _checklistsController.DeleteChecklistAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ─── Attachments ────────────────────────────────────────────────

    [TestMethod]
    public async Task ListAttachments_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var result = await _attachmentsController.ListAttachmentsAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task AddAttachment_ReturnsCreated_WhenValid()
    {
        var (_, cardId) = await CreateBoardListCard();
        var request = new AddAttachmentRequest { FileName = "doc.pdf", Url = "https://example.com/doc.pdf" };
        var result = await _attachmentsController.AddAttachmentAsync(cardId, request);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task RemoveAttachment_ReturnsNotFound_WhenDoesNotExist()
    {
        var result = await _attachmentsController.RemoveAttachmentAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ─── Dependencies ───────────────────────────────────────────────

    [TestMethod]
    public async Task ListDependencies_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var result = await _dependenciesController.ListDependenciesAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task AddDependency_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var request = new AddDependencyRequest { DependsOnCardId = Guid.NewGuid(), Type = CardDependencyType.BlockedBy };
        var result = await _dependenciesController.AddDependencyAsync(Guid.NewGuid(), request);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task RemoveDependency_ReturnsNotFound_WhenDoesNotExist()
    {
        var result = await _dependenciesController.RemoveDependencyAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ─── Time Entries ───────────────────────────────────────────────

    [TestMethod]
    public async Task ListTimeEntries_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var result = await _timeEntriesController.ListTimeEntriesAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task StartTimer_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var result = await _timeEntriesController.StartTimerAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task StopTimer_ReturnsNotFound_WhenCardDoesNotExist()
    {
        var result = await _timeEntriesController.StopTimerAsync(Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task StartTimer_ReturnsOk_WhenValid()
    {
        var (_, cardId) = await CreateBoardListCard();
        var result = await _timeEntriesController.StartTimerAsync(cardId);
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateTimeEntry_ReturnsCreated_WhenValid()
    {
        var (_, cardId) = await CreateBoardListCard();
        var dto = new CreateTimeEntryDto
        {
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            DurationMinutes = 60
        };
        var result = await _timeEntriesController.CreateTimeEntryAsync(cardId, dto);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task DeleteTimeEntry_ReturnsNotFound_WhenDoesNotExist()
    {
        var result = await _timeEntriesController.DeleteTimeEntryAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private async Task<(Guid boardId, Guid cardId)> CreateBoardListCard()
    {
        var caller = TestHelpers.CreateCaller(_userId);
        var board = await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "Board" }, caller);
        var list = await _listService.CreateListAsync(board.Id, new CreateBoardListDto { Title = "Todo" }, caller);
        var card = await _cardService.CreateCardAsync(list.Id, new CreateCardDto
        {
            Title = "Card",
            Priority = CardPriority.None,
            AssigneeIds = [],
            LabelIds = []
        }, caller);
        return (board.Id, card.Id);
    }
}
