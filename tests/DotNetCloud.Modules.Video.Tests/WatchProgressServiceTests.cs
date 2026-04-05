using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class WatchProgressServiceTests
{
    private VideoDbContext _db = null!;
    private WatchProgressService _service = null!;
    private Mock<IEventBus> _eventBus = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBus = new Mock<IEventBus>();
        _service = new WatchProgressService(_db, _eventBus.Object, Mock.Of<ILogger<WatchProgressService>>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task UpdateProgressAsync_CreatesNewProgress()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        var dto = new Core.DTOs.UpdateWatchProgressDto { PositionTicks = TimeSpan.FromMinutes(30).Ticks };

        await _service.UpdateProgressAsync(video.Id, dto, caller);

        var result = await _service.GetProgressAsync(video.Id, caller);
        Assert.IsNotNull(result);
        Assert.AreEqual(TimeSpan.FromMinutes(30).Ticks, result.PositionTicks);
    }

    [TestMethod]
    public async Task UpdateProgressAsync_UpdatesExistingProgress()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        await TestHelpers.SeedWatchProgressAsync(_db, video.Id, caller.UserId, TimeSpan.FromMinutes(10).Ticks);
        var dto = new Core.DTOs.UpdateWatchProgressDto { PositionTicks = TimeSpan.FromMinutes(45).Ticks };

        await _service.UpdateProgressAsync(video.Id, dto, caller);

        var result = await _service.GetProgressAsync(video.Id, caller);
        Assert.IsNotNull(result);
        Assert.AreEqual(TimeSpan.FromMinutes(45).Ticks, result.PositionTicks);
    }

    [TestMethod]
    public async Task UpdateProgressAsync_AutoCompletesAt90Percent()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        // Video is 90 minutes (set by SeedVideoAsync), so 90% = 81 minutes
        var dto = new Core.DTOs.UpdateWatchProgressDto { PositionTicks = TimeSpan.FromMinutes(82).Ticks };

        await _service.UpdateProgressAsync(video.Id, dto, caller);

        var progress = await _db.WatchProgresses.FirstAsync(wp => wp.VideoId == video.Id && wp.UserId == caller.UserId);
        Assert.IsTrue(progress.IsCompleted);
    }

    [TestMethod]
    public async Task GetProgressAsync_ReturnsProgress_WhenExists()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        await TestHelpers.SeedWatchProgressAsync(_db, video.Id, caller.UserId, TimeSpan.FromMinutes(15).Ticks);

        var result = await _service.GetProgressAsync(video.Id, caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(TimeSpan.FromMinutes(15).Ticks, result.PositionTicks);
    }

    [TestMethod]
    public async Task GetProgressAsync_ReturnsNull_WhenNotExists()
    {
        var caller = TestHelpers.CreateCaller();

        var result = await _service.GetProgressAsync(Guid.NewGuid(), caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetContinueWatchingAsync_ReturnsInProgressVideos()
    {
        var caller = TestHelpers.CreateCaller();
        var v1 = await TestHelpers.SeedVideoAsync(_db, "In Progress", ownerId: caller.UserId);
        var v2 = await TestHelpers.SeedVideoAsync(_db, "Completed", ownerId: caller.UserId);
        var v3 = await TestHelpers.SeedVideoAsync(_db, "Also In Progress", ownerId: caller.UserId);

        await TestHelpers.SeedWatchProgressAsync(_db, v1.Id, caller.UserId, TimeSpan.FromMinutes(30).Ticks);
        await TestHelpers.SeedWatchProgressAsync(_db, v2.Id, caller.UserId, 0, isCompleted: true);
        await TestHelpers.SeedWatchProgressAsync(_db, v3.Id, caller.UserId, TimeSpan.FromMinutes(10).Ticks);

        var result = await _service.GetContinueWatchingAsync(caller, 10);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task RecordViewAsync_IncrementsViewCountAndCreatesHistory()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, "CountMe", ownerId: caller.UserId);

        await _service.RecordViewAsync(video.Id, caller, 60);
        await _service.RecordViewAsync(video.Id, caller, 120);

        var updated = await _db.Videos.FindAsync(video.Id);
        Assert.AreEqual(2, updated!.ViewCount);

        var histories = _db.WatchHistories.Where(h => h.VideoId == video.Id).ToList();
        Assert.AreEqual(2, histories.Count);
    }

    [TestMethod]
    public async Task RecordViewAsync_PublishesWatchedEvent()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, "EventVid", ownerId: caller.UserId);

        await _service.RecordViewAsync(video.Id, caller, 300);

        _eventBus.Verify(
            e => e.PublishAsync(It.IsAny<Core.Events.VideoWatchedEvent>(), It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
