using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoIndexingCallbackTests
{
    private VideoDbContext _db = null!;
    private VideoService _videoService = null!;
    private VideoIndexingCallback _callback = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _videoService = new VideoService(_db, Mock.Of<IEventBus>(), Mock.Of<ILogger<VideoService>>());
        _callback = new VideoIndexingCallback(_videoService, _db, Mock.Of<IServiceScopeFactory>(), Mock.Of<IConfiguration>(), Mock.Of<ILogger<VideoIndexingCallback>>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task IndexVideoAsync_CreatesVideoInDatabase()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _callback.IndexVideoAsync(fileNodeId, "movie.mp4", "video/mp4", 500_000_000, ownerId);

        var count = _db.Videos.Count(v => v.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexVideoAsync_SetsCorrectTitle()
    {
        var fileNodeId = Guid.NewGuid();

        await _callback.IndexVideoAsync(fileNodeId, "family-vacation.mkv", "video/x-matroska", 1024, Guid.NewGuid());

        var video = _db.Videos.First(v => v.FileNodeId == fileNodeId);
        Assert.AreEqual("family-vacation", video.Title);
    }

    [TestMethod]
    public async Task IndexVideoAsync_SetsCorrectOwner()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _callback.IndexVideoAsync(fileNodeId, "test.mp4", "video/mp4", 1024, ownerId);

        var video = _db.Videos.First(v => v.FileNodeId == fileNodeId);
        Assert.AreEqual(ownerId, video.OwnerId);
    }

    [TestMethod]
    public async Task IndexVideoAsync_DuplicateFileNode_DoesNotCreateSecond()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _callback.IndexVideoAsync(fileNodeId, "first.mp4", "video/mp4", 1024, ownerId);
        await _callback.IndexVideoAsync(fileNodeId, "second.mp4", "video/mp4", 2048, ownerId);

        var count = _db.Videos.Count(v => v.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexVideoAsync_MultipleUniqueFiles_CreatesAll()
    {
        var ownerId = Guid.NewGuid();

        await _callback.IndexVideoAsync(Guid.NewGuid(), "vid1.mp4", "video/mp4", 1024, ownerId);
        await _callback.IndexVideoAsync(Guid.NewGuid(), "vid2.mkv", "video/x-matroska", 2048, ownerId);
        await _callback.IndexVideoAsync(Guid.NewGuid(), "vid3.webm", "video/webm", 512, ownerId);

        Assert.AreEqual(3, _db.Videos.Count());
    }
}
