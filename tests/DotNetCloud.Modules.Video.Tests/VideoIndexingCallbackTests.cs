using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoIndexingCallbackTests
{
    private VideoDbContext _db = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private VideoService _videoService = null!;
    private Mock<IVideoCollectionService> _collectionServiceMock = null!;
    private Mock<IVideoSeriesService> _seriesServiceMock = null!;
    private VideoIndexingCallback _callback = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _eventBusMock.Setup(x => x.PublishAsync(It.IsAny<VideoAddedEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _eventBusMock.Setup(x => x.PublishAsync(It.IsAny<SearchIndexRequestEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _videoService = new VideoService(_db, _eventBusMock.Object, Mock.Of<IVideoSeriesService>(), Mock.Of<DotNetCloud.Core.Data.Naming.ITableNamingStrategy>(), Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(), Mock.Of<ILogger<VideoService>>());
        _collectionServiceMock = new Mock<IVideoCollectionService>();
        _collectionServiceMock
            .Setup(x => x.FindOrCreateByNameAsync(It.IsAny<string>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CallerContext caller, CancellationToken _) =>
                new VideoCollectionDto
                {
                    Id = Guid.CreateVersion7(),
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                });
        _collectionServiceMock
            .Setup(x => x.AddVideoAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _seriesServiceMock = new Mock<IVideoSeriesService>();
        _callback = new VideoIndexingCallback(_videoService, _collectionServiceMock.Object, _seriesServiceMock.Object, _db, Mock.Of<IConfiguration>(), Mock.Of<ILogger<VideoIndexingCallback>>(), Mock.Of<IServiceScopeFactory>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task IndexVideoAsync_CreatesVideoInDatabase()
    {
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexVideoAsync(fileNodeId, "movie.mp4", "video/mp4", 500_000_000, ownerId);

        var count = _db.UserVideos.Count(v => v.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexVideoAsync_SetsCorrectTitle()
    {
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexVideoAsync(fileNodeId, "family-vacation.mkv", "video/x-matroska", 1024, ownerId);

        var uv = _db.UserVideos.First(v => v.FileNodeId == fileNodeId);
        var canonical = _db.CanonicalVideos.First(cv => cv.ContentHash == uv.CanonicalContentHash);
        Assert.AreEqual("family-vacation", canonical.Title);
    }

    [TestMethod]
    public async Task IndexVideoAsync_SetsCorrectOwner()
    {
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexVideoAsync(fileNodeId, "test.mp4", "video/mp4", 1024, ownerId);

        var uv = _db.UserVideos.First(v => v.FileNodeId == fileNodeId);
        Assert.AreEqual(ownerId, uv.OwnerId);
    }

    [TestMethod]
    public async Task IndexVideoAsync_DuplicateFileNode_DoesNotCreateSecond()
    {
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexVideoAsync(fileNodeId, "first.mp4", "video/mp4", 1024, ownerId);
        await _callback.IndexVideoAsync(fileNodeId, "second.mp4", "video/mp4", 2048, ownerId);

        var count = _db.UserVideos.Count(v => v.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexVideoAsync_MultipleUniqueFiles_CreatesAll()
    {
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexVideoAsync(Guid.CreateVersion7(), "vid1.mp4", "video/mp4", 1024, ownerId);
        await _callback.IndexVideoAsync(Guid.CreateVersion7(), "vid2.mkv", "video/x-matroska", 2048, ownerId);
        await _callback.IndexVideoAsync(Guid.CreateVersion7(), "vid3.webm", "video/webm", 512, ownerId);

        Assert.AreEqual(3, _db.UserVideos.Count());
    }

    [TestMethod]
    public async Task IndexVideoAsync_WithSourceName_CreatesCollectionAndAddsVideo()
    {
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

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
        var fileNodeId = Guid.CreateVersion7();

        await _callback.IndexVideoAsync(fileNodeId, "movie.mp4", "video/mp4", 1024, Guid.CreateVersion7(), sourceName: null);

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
        var fileNodeId = Guid.CreateVersion7();

        await _callback.IndexVideoAsync(fileNodeId, "movie.mp4", "video/mp4", 1024, Guid.CreateVersion7(), sourceName: "");

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
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        _collectionServiceMock
            .Setup(x => x.FindOrCreateByNameAsync(It.IsAny<string>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await _callback.IndexVideoAsync(fileNodeId, "episode.mp4", "video/mp4", 1024, ownerId, sourceName: "TV Shows");

        // Video should still be created despite collection error
        var count = _db.UserVideos.Count(v => v.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexVideoAsync_MultipleSourceNames_UsesEachCorrectly()
    {
        var ownerId = Guid.CreateVersion7();
        var file1 = Guid.CreateVersion7();
        var file2 = Guid.CreateVersion7();

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

    [TestMethod]
    public async Task RemoveDeletedVideosAsync_HardDeletesUserVideoRows()
    {
        var ownerId = Guid.CreateVersion7();
        var fileNodeId = Guid.CreateVersion7();
        var contentHash = "hash-single-episode";

        _db.CanonicalVideos.Add(new CanonicalVideo
        {
            ContentHash = contentHash,
            Title = "Show Episode",
            FileName = "ep.mp4",
            MimeType = "video/mp4",
            SizeBytes = 1000,
        });
        _db.UserVideos.Add(new UserVideo
        {
            OwnerId = ownerId,
            FileNodeId = fileNodeId,
            CanonicalContentHash = contentHash,
        });
        await _db.SaveChangesAsync();

        var removed = await _callback.RemoveDeletedVideosAsync([fileNodeId], ownerId);

        Assert.AreEqual(1, removed);
        // Hard delete: the row must be gone entirely (even ignoring query filters), so the
        // unique (FileNodeId, OwnerId) index slot is freed for a later re-import.
        Assert.AreEqual(0, _db.UserVideos.IgnoreQueryFilters().Count(uv => uv.FileNodeId == fileNodeId));
    }

    [TestMethod]
    public async Task RemoveDeletedVideosAsync_ContentAlsoInAnotherSource_KeepsCopyAndSeriesMembership()
    {
        var ownerId = Guid.CreateVersion7();
        var sourceAFile = Guid.CreateVersion7();
        var sourceBFile = Guid.CreateVersion7();
        var contentHash = "hash-shared-show";

        _db.CanonicalVideos.Add(new CanonicalVideo
        {
            ContentHash = contentHash,
            Title = "Shared Episode",
            FileName = "shared.mp4",
            MimeType = "video/mp4",
        });
        var series = new CanonicalVideoSeries { Name = "Shared Show", Type = SeriesType.TvSeries };
        var season = new CanonicalVideoSeason { Series = series, SeasonNumber = 1 };
        _db.CanonicalVideoSeries.Add(series);
        _db.CanonicalVideoSeasons.Add(season);
        _db.CanonicalVideoEpisodes.Add(new CanonicalVideoEpisode
        {
            Season = season,
            VideoContentHash = contentHash,
            EpisodeNumber = 1,
        });
        _db.UserVideos.Add(new UserVideo { OwnerId = ownerId, FileNodeId = sourceAFile, CanonicalContentHash = contentHash });
        _db.UserVideos.Add(new UserVideo { OwnerId = ownerId, FileNodeId = sourceBFile, CanonicalContentHash = contentHash });
        await _db.SaveChangesAsync();

        // Remove source A only; the identical copy indexed under source B must survive.
        var removed = await _callback.RemoveDeletedVideosAsync([sourceAFile], ownerId);

        Assert.AreEqual(1, removed);
        Assert.AreEqual(0, _db.UserVideos.Count(uv => uv.FileNodeId == sourceAFile));
        Assert.AreEqual(1, _db.UserVideos.Count(uv => uv.FileNodeId == sourceBFile));
        // Canonical series membership survives because the content is still owned via source B.
        Assert.AreEqual(1, _db.CanonicalVideoEpisodes.Count(e => e.VideoContentHash == contentHash));
    }

    [TestMethod]
    public async Task RemoveDeletedVideosAsync_LastCopyRemoved_CleansOrphanedSeriesEpisode()
    {
        var ownerId = Guid.CreateVersion7();
        var fileNodeId = Guid.CreateVersion7();
        var contentHash = "hash-last-copy";

        _db.CanonicalVideos.Add(new CanonicalVideo
        {
            ContentHash = contentHash,
            Title = "Only Episode",
            FileName = "only.mp4",
            MimeType = "video/mp4",
        });
        var series = new CanonicalVideoSeries { Name = "Only Show", Type = SeriesType.TvSeries };
        var season = new CanonicalVideoSeason { Series = series, SeasonNumber = 1 };
        _db.CanonicalVideoSeries.Add(series);
        _db.CanonicalVideoSeasons.Add(season);
        _db.CanonicalVideoEpisodes.Add(new CanonicalVideoEpisode
        {
            Season = season,
            VideoContentHash = contentHash,
            EpisodeNumber = 1,
        });
        _db.UserVideos.Add(new UserVideo { OwnerId = ownerId, FileNodeId = fileNodeId, CanonicalContentHash = contentHash });
        await _db.SaveChangesAsync();

        var removed = await _callback.RemoveDeletedVideosAsync([fileNodeId], ownerId);

        Assert.AreEqual(1, removed);
        Assert.AreEqual(0, _db.UserVideos.Count(uv => uv.FileNodeId == fileNodeId));
        // With no surviving copy anywhere, the orphaned canonical episode is cleaned up.
        Assert.AreEqual(0, _db.CanonicalVideoEpisodes.Count(e => e.VideoContentHash == contentHash));
    }

    [TestMethod]
    public async Task IndexVideoAsync_StaleTombstoneForFileNode_IsPurgedOnReimport()
    {
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        await _callback.IndexVideoAsync(fileNodeId, "ep.mp4", "video/mp4", 1024, ownerId);

        // Simulate the leftover soft-deleted tombstone the old removal path left behind.
        var tombstone = _db.UserVideos.IgnoreQueryFilters().First(uv => uv.FileNodeId == fileNodeId);
        tombstone.IsDeleted = true;
        tombstone.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Re-importing (e.g. re-adding the source + Scan Now) must purge the tombstone first,
        // otherwise the insert collides with the unique (FileNodeId, OwnerId) index.
        await _callback.IndexVideoAsync(fileNodeId, "ep.mp4", "video/mp4", 1024, ownerId);

        Assert.AreEqual(0, _db.UserVideos.IgnoreQueryFilters().Count(uv => uv.FileNodeId == fileNodeId && uv.IsDeleted));
        Assert.AreEqual(1, _db.UserVideos.Count(uv => uv.FileNodeId == fileNodeId));
    }
}
