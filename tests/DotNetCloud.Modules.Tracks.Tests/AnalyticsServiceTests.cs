using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class AnalyticsServiceTests
{
    private TracksDbContext _db = null!;
    private AnalyticsService _service = null!;
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
        _service = new AnalyticsService(_db, boardService, NullLogger<AnalyticsService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── GetBoardAnalytics ────────────────────────────────────────────

    [TestMethod]
    public async Task GetBoardAnalytics_EmptyBoard_ReturnsZeroStats()
    {
        var result = await _service.GetBoardAnalyticsAsync(_board.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.TotalCards);
        Assert.AreEqual(0, result.CompletedCards);
    }

    [TestMethod]
    public async Task GetBoardAnalytics_WithCards_ReturnsCounts()
    {
        await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "Card 1");
        await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "Card 2");
        var archived = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "Done Card");
        archived.IsArchived = true;
        await _db.SaveChangesAsync();

        var result = await _service.GetBoardAnalyticsAsync(_board.Id, _caller);

        Assert.AreEqual(3, result.TotalCards);
        Assert.AreEqual(1, result.CompletedCards);
    }

    [TestMethod]
    public async Task GetBoardAnalytics_NonMember_Throws()
    {
        var outsider = TestHelpers.CreateCaller();

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.GetBoardAnalyticsAsync(_board.Id, outsider));
    }

    [TestMethod]
    public async Task GetBoardAnalytics_WithCardsAndDates_ReturnsCompletionTrend()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId);
        card.IsArchived = true;
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var result = await _service.GetBoardAnalyticsAsync(_board.Id, _caller, daysBack: 7);

        Assert.IsTrue(result.CompletionsOverTime.Any());
    }

    // ─── GetTeamAnalytics ─────────────────────────────────────────────

    [TestMethod]
    public async Task GetTeamAnalytics_NonMember_Throws()
    {
        // Caller is not in the team — service throws TRACKS_NOT_TEAM_MEMBER
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.GetTeamAnalyticsAsync(Guid.NewGuid(), _caller));
    }
}
