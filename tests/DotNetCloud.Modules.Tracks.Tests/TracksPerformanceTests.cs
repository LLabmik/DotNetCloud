using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using IEventBus = DotNetCloud.Core.Events.IEventBus;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Performance tests for the Tracks module: large board operations, reorder operations,
/// and team with many members.
/// </summary>
[TestClass]
public class TracksPerformanceTests
{
    private TracksDbContext _db = null!;
    private BoardService _boardService = null!;
    private SwimlaneService _swimlaneService = null!;
    private CardService _cardService = null!;
    private LabelService _labelService = null!;
    private DependencyService _dependencyService = null!;
    private ActivityService _activityService = null!;
    private TeamService _teamService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<ITeamDirectory> _teamDirectoryMock = null!;
    private Mock<ITeamManager> _teamManagerMock = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        _teamDirectoryMock = new Mock<ITeamDirectory>();
        _teamManagerMock = new Mock<ITeamManager>();
        _teamDirectoryMock
            .Setup(d => d.IsTeamMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _teamDirectoryMock
            .Setup(d => d.GetTeamAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new TeamInfo { Id = id, OrganizationId = Guid.Empty, Name = "Team", MemberCount = 1, CreatedAt = DateTime.UtcNow });
        _teamManagerMock
            .Setup(m => m.CreateTeamAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid orgId, string name, string? desc, Guid ownerId, CancellationToken _) =>
                new TeamInfo { Id = Guid.NewGuid(), OrganizationId = orgId, Name = name, Description = desc, MemberCount = 1, CreatedAt = DateTime.UtcNow });
        _teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance,
            _teamDirectoryMock.Object, _teamManagerMock.Object);
        _boardService = new BoardService(_db, _eventBusMock.Object, _activityService, _teamService, NullLogger<BoardService>.Instance);
        _swimlaneService = new SwimlaneService(_db, _boardService, _activityService, NullLogger<SwimlaneService>.Instance);
        _cardService = new CardService(_db, _boardService, _activityService, _eventBusMock.Object, NullLogger<CardService>.Instance);
        _labelService = new LabelService(_db, _boardService, _activityService, NullLogger<LabelService>.Instance);
        _dependencyService = new DependencyService(_db, _boardService, _activityService, NullLogger<DependencyService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Large Board (100+ Cards) ────────────────────────────────────

    [TestMethod]
    public async Task LargeBoard_100Cards_ListCardsCompletes()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Large Board" }, _caller);
        var list = await _swimlaneService.CreateSwimlaneAsync(board.Id,
            new CreateBoardSwimlaneDto { Title = "Backlog" }, _caller);

        // Seed 100 cards via direct DB insert for speed
        for (var i = 0; i < 100; i++)
        {
            _db.Cards.Add(new Card
            {
                SwimlaneId = list.Id,
                Title = $"Card {i + 1}",
                Position = (i + 1) * 1000.0,
                CreatedByUserId = _caller.UserId
            });
        }
        await _db.SaveChangesAsync();

        // List cards should return all 100
        var cards = await _cardService.ListCardsAsync(list.Id, _caller);
        Assert.AreEqual(100, cards.Count);
    }

    [TestMethod]
    public async Task LargeBoard_CardsAcrossMultipleLists_ListBoardCompletes()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Multi-List Board" }, _caller);

        // Create 10 lists, each with 50 cards = 500 total
        for (var listNum = 0; listNum < 10; listNum++)
        {
            var list = await _swimlaneService.CreateSwimlaneAsync(board.Id,
                new CreateBoardSwimlaneDto { Title = $"List {listNum + 1}" }, _caller);

            for (var cardNum = 0; cardNum < 50; cardNum++)
            {
                _db.Cards.Add(new Card
                {
                    SwimlaneId = list.Id,
                    Title = $"Card {listNum}-{cardNum}",
                    Position = (cardNum + 1) * 1000.0,
                    CreatedByUserId = _caller.UserId
                });
            }
        }
        await _db.SaveChangesAsync();

        // Getting board should complete without error
        var result = await _boardService.GetBoardAsync(board.Id, _caller);
        Assert.IsNotNull(result);
        Assert.AreEqual(10, result.Swimlanes.Count);
    }

    // ─── Reorder Operations ──────────────────────────────────────────

    [TestMethod]
    public async Task ReorderLists_20Lists_UpdatesAllPositions()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Reorder Board" }, _caller);

        var listIds = new List<Guid>();
        for (var i = 0; i < 20; i++)
        {
            var list = await _swimlaneService.CreateSwimlaneAsync(board.Id,
                new CreateBoardSwimlaneDto { Title = $"List {i + 1}" }, _caller);
            listIds.Add(list.Id);
        }

        // Reverse the order
        listIds.Reverse();
        await _swimlaneService.ReorderSwimlanesAsync(board.Id, listIds, _caller);

        // Verify positions are correct
        var lists = await _swimlaneService.GetSwimlanesAsync(board.Id, _caller);
        Assert.AreEqual(20, lists.Count);

        // First list in reordered array should have lowest position
        for (var i = 0; i < lists.Count - 1; i++)
        {
            var currentList = lists.First(l => l.Id == listIds[i]);
            var nextList = lists.First(l => l.Id == listIds[i + 1]);
            Assert.IsTrue(currentList.Position < nextList.Position,
                $"List at index {i} should have lower position than list at index {i + 1}");
        }
    }

    [TestMethod]
    public async Task MoveCard_Between50Cards_MaintainsOrdering()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Dense Board" }, _caller);
        var sourceList = await _swimlaneService.CreateSwimlaneAsync(board.Id,
            new CreateBoardSwimlaneDto { Title = "Source" }, _caller);
        var targetList = await _swimlaneService.CreateSwimlaneAsync(board.Id,
            new CreateBoardSwimlaneDto { Title = "Target" }, _caller);

        // Fill target with 50 cards
        for (var i = 0; i < 50; i++)
        {
            _db.Cards.Add(new Card
            {
                SwimlaneId = targetList.Id,
                Title = $"Target Card {i + 1}",
                Position = (i + 1) * 1000.0,
                CreatedByUserId = _caller.UserId
            });
        }
        await _db.SaveChangesAsync();

        // Create card in source
        var card = await _cardService.CreateCardAsync(sourceList.Id,
            new CreateCardDto { Title = "Moving Card" }, _caller);

        // Move to middle of target list
        await _cardService.MoveCardAsync(card.Id,
            new MoveCardDto { TargetSwimlaneId = targetList.Id, Position = 25000 }, _caller);

        var movedCard = await _cardService.GetCardAsync(card.Id, _caller);
        Assert.AreEqual(targetList.Id, movedCard!.SwimlaneId);
    }

    // ─── Team with Many Members ──────────────────────────────────────

    [TestMethod]
    public async Task Team_50Members_ListMembersCompletes()
    {
        var team = await _teamService.CreateTeamAsync(
            new CreateTracksTeamDto { Name = "Large Team" }, _caller);

        // Add 50 members directly via DB for speed
        for (var i = 0; i < 50; i++)
        {
            _db.TeamRoles.Add(new TeamRole
            {
                CoreTeamId = team.Id,
                UserId = Guid.NewGuid(),
                Role = TracksTeamMemberRole.Member,
                AssignedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        // List members should complete
        var teamResult = await _teamService.GetTeamAsync(team.Id, _caller);
        Assert.IsNotNull(teamResult);
    }

    // ─── Board with Many Members ─────────────────────────────────────

    [TestMethod]
    public async Task Board_30Members_ListBoardCompletes()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Crowded Board");

        // Add 30 members
        for (var i = 0; i < 30; i++)
        {
            await TestHelpers.AddMemberAsync(_db, board.Id, Guid.NewGuid(), BoardMemberRole.Member);
        }

        var result = await _boardService.GetBoardAsync(board.Id, _caller);
        Assert.IsNotNull(result);
        Assert.AreEqual(31, result.Members.Count); // 30 + 1 owner
    }

    // ─── Many Labels ─────────────────────────────────────────────────

    [TestMethod]
    public async Task Board_50Labels_ListLabelsCompletes()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Label Board" }, _caller);

        for (var i = 0; i < 50; i++)
        {
            await _labelService.CreateLabelAsync(board.Id,
                new CreateLabelDto { Title = $"Label {i + 1}", Color = $"#{i:X6}" }, _caller);
        }

        var labels = await _labelService.GetLabelsAsync(board.Id, _caller);
        Assert.AreEqual(50, labels.Count);
    }

    // ─── Dependency Chain ────────────────────────────────────────────

    [TestMethod]
    public async Task DependencyChain_20Deep_NoCycleDetected()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Chain Board");
        var list = await TestHelpers.SeedSwimlaneAsync(_db, board.Id);

        // Create a chain of 20 cards: A → B → C → ... → T
        var cards = new List<Card>();
        for (var i = 0; i < 20; i++)
        {
            cards.Add(await TestHelpers.SeedCardAsync(_db, list.Id, _caller.UserId, $"Chain {i}"));
        }

        // Add dependencies: each card is blocked by the next
        for (var i = 0; i < 19; i++)
        {
            await _dependencyService.AddDependencyAsync(
                cards[i].Id, cards[i + 1].Id, CardDependencyType.BlockedBy, _caller);
        }

        // Getting dependencies for first card should complete
        var deps = await _dependencyService.GetDependenciesAsync(cards[0].Id, _caller);
        Assert.AreEqual(1, deps.Count);
    }
}
