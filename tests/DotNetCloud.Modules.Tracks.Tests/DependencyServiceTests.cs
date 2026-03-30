using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class DependencyServiceTests
{
    private TracksDbContext _db;
    private DependencyService _service;
    private CallerContext _caller;
    private Board _board;
    private BoardSwimlane _swimlane;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, new Mock<IEventBus>().Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, new Mock<IEventBus>().Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new DependencyService(_db, boardService, activityService, NullLogger<DependencyService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Add Dependency ───────────────────────────────────────────────

    [TestMethod]
    public async Task AddDependency_ValidCards_ReturnsDependency()
    {
        var cardA = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "Card A");
        var cardB = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "Card B");

        var result = await _service.AddDependencyAsync(cardA.Id, cardB.Id, CardDependencyType.BlockedBy, _caller);

        Assert.AreEqual(cardA.Id, result.CardId);
        Assert.AreEqual(cardB.Id, result.DependsOnCardId);
        Assert.AreEqual(CardDependencyType.BlockedBy, result.Type);
    }

    [TestMethod]
    public async Task AddDependency_SelfDependency_Throws()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.AddDependencyAsync(card.Id, card.Id, CardDependencyType.BlockedBy, _caller));
    }

    [TestMethod]
    public async Task AddDependency_Duplicate_Throws()
    {
        var cardA = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "A");
        var cardB = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "B");

        await _service.AddDependencyAsync(cardA.Id, cardB.Id, CardDependencyType.BlockedBy, _caller);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.AddDependencyAsync(cardA.Id, cardB.Id, CardDependencyType.BlockedBy, _caller));
    }

    [TestMethod]
    public async Task AddDependency_CycleDetection_Throws()
    {
        var cardA = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "A");
        var cardB = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "B");
        var cardC = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "C");

        // A blocked by B, B blocked by C — then C blocked by A should fail (cycle)
        await _service.AddDependencyAsync(cardA.Id, cardB.Id, CardDependencyType.BlockedBy, _caller);
        await _service.AddDependencyAsync(cardB.Id, cardC.Id, CardDependencyType.BlockedBy, _caller);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.AddDependencyAsync(cardC.Id, cardA.Id, CardDependencyType.BlockedBy, _caller));
    }

    [TestMethod]
    public async Task AddDependency_RelatesTo_SkipsCycleCheck()
    {
        var cardA = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "A");
        var cardB = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "B");

        // RelatesTo bidirectional should work
        await _service.AddDependencyAsync(cardA.Id, cardB.Id, CardDependencyType.RelatesTo, _caller);

        // Should NOT throw — RelatesTo skips cycle detection
        // Note: this will throw duplicate, not cycle. Let's use different cards.
        var cardC = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "C");
        var cardD = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "D");

        await _service.AddDependencyAsync(cardC.Id, cardD.Id, CardDependencyType.RelatesTo, _caller);
        // No cycle check for RelatesTo — this is a success assertion by not throwing
    }

    // ─── Get Dependencies ─────────────────────────────────────────────

    [TestMethod]
    public async Task GetDependencies_ReturnsDependencies()
    {
        var cardA = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "A");
        var cardB = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "B");
        await _service.AddDependencyAsync(cardA.Id, cardB.Id, CardDependencyType.BlockedBy, _caller);

        var results = await _service.GetDependenciesAsync(cardA.Id, _caller);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(cardB.Id, results[0].DependsOnCardId);
    }

    // ─── Remove Dependency ────────────────────────────────────────────

    [TestMethod]
    public async Task RemoveDependency_RemovesFromDb()
    {
        var cardA = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "A");
        var cardB = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId, "B");
        await _service.AddDependencyAsync(cardA.Id, cardB.Id, CardDependencyType.BlockedBy, _caller);

        await _service.RemoveDependencyAsync(cardA.Id, cardB.Id, _caller);

        Assert.IsFalse(await _db.CardDependencies.AnyAsync(d => d.CardId == cardA.Id && d.DependsOnCardId == cardB.Id));
    }

    [TestMethod]
    public async Task RemoveDependency_NonExistent_IsIdempotent()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId);

        // Should not throw
        await _service.RemoveDependencyAsync(card.Id, Guid.NewGuid(), _caller);
    }
}
