using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
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
    private Mock<IVideoCollectionService> _collectionServiceMock = null!;
    private Mock<IVideoSeriesService> _seriesServiceMock = null!;
    private VideoIndexingCallback _callback = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _videoService = new VideoService(_db, Mock.Of<IEventBus>(), Mock.Of<IVideoSeriesService>(), Mock.Of<ILogger<VideoService>>());
        _collectionServiceMock = new Mock<IVideoCollectionService>();
        _collectionServiceMock
            .Setup(x => x.FindOrCreateByNameAsync(It.IsAny<string>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CallerContext caller, CancellationToken _) =>
                new VideoCollectionDto
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                });
        _collectionServiceMock
            .Setup(x => x.AddVideoAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _seriesServiceMock = new Mock<IVideoSeriesService>();
        _callback = new VideoIndexingCallback(_videoService, _collectionServiceMock.Object, _seriesServiceMock.Object, _db, Mock.Of<IServiceScopeFactory>(), Mock.Of<IConfiguration>(), Mock.Of<ILogger<VideoIndexingCallback>>());
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

    [TestMethod]
    public async Task IndexVideoAsync_WithSourceName_CreatesCollectionAndAddsVideo()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _callback.IndexVideoAsync(fileNodeId, "episode.mp4", "video/mp4", 1024, ownerId, sourceName: "TV Shows");

        _collectionServiceMock.Verify(
            x => x.FindOrCreateByNameAsync("TV Shows", It.Is<CallerContext>(c => c.UserId == ownerId), It.IsAny<CancellationToken>()),
            Times.Once);
        _collectionServiceMock.Verify(
            x => x.AddVideoAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.Is<CallerContext>(c => c.UserId == ownerId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task IndexVideoAsync_WithNullSourceName_DoesNotCreateCollection()
    {
        var fileNodeId = Guid.NewGuid();

        await _callback.IndexVideoAsync(fileNodeId, "movie.mp4", "video/mp4", 1024, Guid.NewGuid(), sourceName: null);

        _collectionServiceMock.Verify(
            x => x.FindOrCreateByNameAsync(It.IsAny<string>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _collectionServiceMock.Verify(
            x => x.AddVideoAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task IndexVideoAsync_WithEmptySourceName_DoesNotCreateCollection()
    {
        var fileNodeId = Guid.NewGuid();

        await _callback.IndexVideoAsync(fileNodeId, "movie.mp4", "video/mp4", 1024, Guid.NewGuid(), sourceName: "");

        _collectionServiceMock.Verify(
            x => x.FindOrCreateByNameAsync(It.IsAny<string>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _collectionServiceMock.Verify(
            x => x.AddVideoAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task IndexVideoAsync_WithSourceName_FindOrCreateFails_VideoStillIndexed()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        _collectionServiceMock
            .Setup(x => x.FindOrCreateByNameAsync(It.IsAny<string>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await _callback.IndexVideoAsync(fileNodeId, "episode.mp4", "video/mp4", 1024, ownerId, sourceName: "TV Shows");

        // Video should still be created despite collection error
        var count = _db.Videos.Count(v => v.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexVideoAsync_MultipleSourceNames_UsesEachCorrectly()
    {
        var ownerId = Guid.NewGuid();
        var file1 = Guid.NewGuid();
        var file2 = Guid.NewGuid();

        await _callback.IndexVideoAsync(file1, "ep1.mp4", "video/mp4", 1024, ownerId, sourceName: "TV Shows");
        await _callback.IndexVideoAsync(file2, "movie.mp4", "video/mp4", 2048, ownerId, sourceName: "Movies");

        _collectionServiceMock.Verify(
            x => x.FindOrCreateByNameAsync("TV Shows", It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _collectionServiceMock.Verify(
            x => x.FindOrCreateByNameAsync("Movies", It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _collectionServiceMock.Verify(
            x => x.AddVideoAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
