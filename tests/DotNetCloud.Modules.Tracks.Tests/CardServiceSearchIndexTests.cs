using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Tests that <see cref="CardService"/> publishes <see cref="SearchIndexRequestEvent"/>
/// on create, update, move, and delete operations.
/// </summary>
[TestClass]
public class CardServiceSearchIndexTests
{
    private TracksDbContext _db = null!;
    private CardService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;
    private Board _board = null!;
    private BoardSwimlane _swimlane = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        _eventBusMock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, _eventBusMock.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new CardService(_db, boardService, activityService, _eventBusMock.Object, NullLogger<CardService>.Instance);

        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        _eventBusMock.Invocations.Clear();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateCard_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var dto = new CreateCardDto
        {
            Title = "Test Card",
            AssigneeIds = [],
            LabelIds = []
        };

        var result = await _service.CreateCardAsync(_swimlane.Id, dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "tracks" &&
                    e.EntityId == result.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task UpdateCard_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var created = await _service.CreateCardAsync(_swimlane.Id,
            new CreateCardDto { Title = "Original", AssigneeIds = [], LabelIds = [] }, _caller);
        _eventBusMock.Invocations.Clear();

        await _service.UpdateCardAsync(created.Id,
            new UpdateCardDto { Title = "Updated" }, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "tracks" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task MoveCard_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var created = await _service.CreateCardAsync(_swimlane.Id,
            new CreateCardDto { Title = "To Move", AssigneeIds = [], LabelIds = [] }, _caller);
        var targetSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id, "Target");
        _eventBusMock.Invocations.Clear();

        await _service.MoveCardAsync(created.Id,
            new MoveCardDto { TargetSwimlaneId = targetSwimlane.Id, Position = 1000 }, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "tracks" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteCard_PublishesSearchIndexRequestEvent_WithRemoveAction()
    {
        var created = await _service.CreateCardAsync(_swimlane.Id,
            new CreateCardDto { Title = "To Delete", AssigneeIds = [], LabelIds = [] }, _caller);
        _eventBusMock.Invocations.Clear();

        await _service.DeleteCardAsync(created.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "tracks" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Remove),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
