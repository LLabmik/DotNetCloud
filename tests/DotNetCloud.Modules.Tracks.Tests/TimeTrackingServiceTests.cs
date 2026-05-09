using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class TimeTrackingServiceTests
{
    private TracksDbContext _db = null!;
    private TimeTrackingService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new TimeTrackingService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task AddManualEntryAsync_CreatesTimeEntry()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var dto = new CreateTimeEntryDto { DurationMinutes = 30, Description = "Development" };

        var result = await _service.AddManualEntryAsync(item.Id, userId, dto, CancellationToken.None);

        Assert.AreEqual(30, result.DurationMinutes);
        Assert.AreEqual("Development", result.Description);
        Assert.AreEqual(userId, result.UserId);
    }

    [TestMethod]
    public async Task GetTimeEntriesByWorkItemAsync_ReturnsEntries()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        await _service.AddManualEntryAsync(item.Id, userId, new CreateTimeEntryDto { DurationMinutes = 15 }, CancellationToken.None);
        await _service.AddManualEntryAsync(item.Id, userId, new CreateTimeEntryDto { DurationMinutes = 45 }, CancellationToken.None);

        var result = await _service.GetTimeEntriesByWorkItemAsync(item.Id, CancellationToken.None);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetTimeEntriesByUserAsync_ReturnsUserEntries()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        await _service.AddManualEntryAsync(item.Id, userId, new CreateTimeEntryDto { DurationMinutes = 30 }, CancellationToken.None);

        var result = await _service.GetTimeEntriesByUserAsync(userId, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task DeleteEntryAsync_RemovesEntry()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var entry = await _service.AddManualEntryAsync(item.Id, userId, new CreateTimeEntryDto { DurationMinutes = 30 }, CancellationToken.None);

        await _service.DeleteEntryAsync(entry.Id, CancellationToken.None);

        var result = await _service.GetTimeEntriesByWorkItemAsync(item.Id, CancellationToken.None);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task StartTimerAsync_CreatesActiveTimer()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();

        var result = await _service.StartTimerAsync(item.Id, userId, CancellationToken.None);

        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(item.Id, result.WorkItemId);
        Assert.IsNotNull(result.StartTime);
        Assert.IsNull(result.EndTime);
    }

    [TestMethod]
    public async Task StopTimerAsync_StopsActiveTimer()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        await _service.StartTimerAsync(item.Id, userId, CancellationToken.None);

        var result = await _service.StopTimerAsync(item.Id, userId, CancellationToken.None);

        Assert.IsNotNull(result.EndTime);
        Assert.IsTrue(result.DurationMinutes > 0);
    }

    [TestMethod]
    public async Task GetActiveTimerAsync_ReturnsRunningTimer()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        await _service.StartTimerAsync(item.Id, userId, CancellationToken.None);

        var result = await _service.GetActiveTimerAsync(userId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.StartTime);
        Assert.IsNull(result.EndTime);
    }

    [TestMethod]
    public async Task GetActiveTimerAsync_NoActiveTimer_ReturnsNull()
    {
        var userId = Guid.NewGuid();

        var result = await _service.GetActiveTimerAsync(userId, CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetTotalMinutesForWorkItemAsync_ReturnsTotal()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        await _service.AddManualEntryAsync(item.Id, userId, new CreateTimeEntryDto { DurationMinutes = 30 }, CancellationToken.None);
        await _service.AddManualEntryAsync(item.Id, userId, new CreateTimeEntryDto { DurationMinutes = 45 }, CancellationToken.None);

        var result = await _service.GetTotalMinutesForWorkItemAsync(item.Id, CancellationToken.None);

        Assert.AreEqual(75, result);
    }
}
