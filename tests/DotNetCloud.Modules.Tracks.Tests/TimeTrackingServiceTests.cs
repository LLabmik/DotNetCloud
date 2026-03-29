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
public class TimeTrackingServiceTests
{
    private TracksDbContext _db;
    private TimeTrackingService _service;
    private CallerContext _caller;
    private Board _board;
    private Card _card;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, new Mock<IEventBus>().Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, new Mock<IEventBus>().Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new TimeTrackingService(_db, boardService, activityService, NullLogger<TimeTrackingService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        var list = await TestHelpers.SeedListAsync(_db, _board.Id);
        _card = await TestHelpers.SeedCardAsync(_db, list.Id, _caller.UserId);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Manual Entry ─────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateTimeEntry_WithDuration_ReturnsEntry()
    {
        var dto = new CreateTimeEntryDto
        {
            StartTime = DateTime.UtcNow.AddHours(-2),
            DurationMinutes = 120,
            Description = "Worked on feature"
        };

        var result = await _service.CreateTimeEntryAsync(_card.Id, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(120, result.DurationMinutes);
        Assert.AreEqual("Worked on feature", result.Description);
        Assert.AreEqual(_caller.UserId, result.UserId);
    }

    [TestMethod]
    public async Task CreateTimeEntry_WithEndTime_CalculatesDuration()
    {
        var start = DateTime.UtcNow.AddHours(-3);
        var end = DateTime.UtcNow;
        var dto = new CreateTimeEntryDto
        {
            StartTime = start,
            EndTime = end
        };

        var result = await _service.CreateTimeEntryAsync(_card.Id, dto, _caller);

        Assert.IsTrue(result.DurationMinutes >= 179); // ~180 minutes
    }

    [TestMethod]
    public async Task CreateTimeEntry_EndBeforeStart_Throws()
    {
        var dto = new CreateTimeEntryDto
        {
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(-1)
        };

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.CreateTimeEntryAsync(_card.Id, dto, _caller));
    }

    [TestMethod]
    public async Task CreateTimeEntry_NoDurationNoEndTime_Throws()
    {
        var dto = new CreateTimeEntryDto { StartTime = DateTime.UtcNow };

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.CreateTimeEntryAsync(_card.Id, dto, _caller));
    }

    // ─── Timer ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task StartTimer_CreatesRunningEntry()
    {
        var result = await _service.StartTimerAsync(_card.Id, _caller);

        Assert.IsNotNull(result);
        Assert.IsNull(result.EndTime);
        Assert.AreEqual(0, result.DurationMinutes);
    }

    [TestMethod]
    public async Task StartTimer_AlreadyRunning_Throws()
    {
        await _service.StartTimerAsync(_card.Id, _caller);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.StartTimerAsync(_card.Id, _caller));
    }

    [TestMethod]
    public async Task StopTimer_CalculatesDuration()
    {
        // Seed a timer that started 5 minutes ago
        var entry = new TimeEntry
        {
            CardId = _card.Id,
            UserId = _caller.UserId,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            DurationMinutes = 0
        };
        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync();

        var result = await _service.StopTimerAsync(_card.Id, _caller);

        Assert.IsNotNull(result.EndTime);
        Assert.IsTrue(result.DurationMinutes >= 1); // Minimum 1 minute
    }

    [TestMethod]
    public async Task StopTimer_NoRunningTimer_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.StopTimerAsync(_card.Id, _caller));
    }

    // ─── Get Entries ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTimeEntries_ReturnsEntries()
    {
        await _service.CreateTimeEntryAsync(_card.Id, new CreateTimeEntryDto { StartTime = DateTime.UtcNow, DurationMinutes = 30 }, _caller);
        await _service.CreateTimeEntryAsync(_card.Id, new CreateTimeEntryDto { StartTime = DateTime.UtcNow, DurationMinutes = 60 }, _caller);

        var results = await _service.GetTimeEntriesAsync(_card.Id, _caller);

        Assert.AreEqual(2, results.Count);
    }

    // ─── Total Minutes ────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTotalMinutes_SumsCorrectly()
    {
        await _service.CreateTimeEntryAsync(_card.Id, new CreateTimeEntryDto { StartTime = DateTime.UtcNow, DurationMinutes = 30 }, _caller);
        await _service.CreateTimeEntryAsync(_card.Id, new CreateTimeEntryDto { StartTime = DateTime.UtcNow, DurationMinutes = 45 }, _caller);

        var total = await _service.GetTotalMinutesAsync(_card.Id);

        Assert.AreEqual(75, total);
    }

    // ─── Delete Entry ─────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteTimeEntry_AsOwner_Deletes()
    {
        var entry = await _service.CreateTimeEntryAsync(_card.Id, new CreateTimeEntryDto { StartTime = DateTime.UtcNow, DurationMinutes = 30 }, _caller);

        await _service.DeleteTimeEntryAsync(entry.Id, _caller);

        Assert.IsFalse(await _db.TimeEntries.AnyAsync(t => t.Id == entry.Id));
    }

    [TestMethod]
    public async Task DeleteTimeEntry_AsOtherUser_Throws()
    {
        var entry = await _service.CreateTimeEntryAsync(_card.Id, new CreateTimeEntryDto { StartTime = DateTime.UtcNow, DurationMinutes = 30 }, _caller);

        var otherCaller = TestHelpers.CreateCaller();

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.DeleteTimeEntryAsync(entry.Id, otherCaller));
    }
}
