using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoEnrichmentBackgroundServiceTests
{
    private VideoDbContext _db = null!;
    private VideoEnrichmentBackgroundService _service = null!;
    private VideoScanProgressState _scanProgress = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _scanProgress = new VideoScanProgressState();
        var queue = new InMemoryVideoEnrichmentBackgroundQueue(
            NullLogger<InMemoryVideoEnrichmentBackgroundQueue>.Instance);

        _service = new VideoEnrichmentBackgroundService(
            Mock.Of<IServiceScopeFactory>(),
            queue,
            _scanProgress,
            NullLogger<VideoEnrichmentBackgroundService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task<UserVideo> SeedVideoAsync(Guid ownerId)
    {
        var contentHash = Guid.CreateVersion7().ToString("N");
        var canonical = new CanonicalVideo
        {
            ContentHash = contentHash,
            Title = $"Video {contentHash[..8]}",
            FileName = $"video-{contentHash[..8]}.mp4",
            MimeType = "video/mp4",
            SizeBytes = 1024
        };
        _db.CanonicalVideos.Add(canonical);

        var userVideo = new UserVideo
        {
            OwnerId = ownerId,
            FileNodeId = Guid.CreateVersion7(),
            CanonicalContentHash = contentHash
        };
        _db.UserVideos.Add(userVideo);
        await _db.SaveChangesAsync();

        return userVideo;
    }

    private static VideoEnrichmentJob CreateFastJob(Guid ownerId, IReadOnlyList<Guid> videoIds) => new()
    {
        OwnerId = ownerId,
        FetchPosters = true,
        FetchMetadata = true,
        StartedAtUtc = DateTimeOffset.UtcNow,
        VideoIds = videoIds,
        IsFastTrack = true
    };

    [TestMethod]
    public async Task RunFastTrackAsync_ScopedToSpecifiedVideos_EnrichesOnlyThose()
    {
        var owner = Guid.NewGuid();
        var videoA = await SeedVideoAsync(owner);
        var videoB = await SeedVideoAsync(owner);
        var videoC = await SeedVideoAsync(owner);

        var enrichmentMock = new Mock<IVideoEnrichmentService>();
        var thumbnailMock = new Mock<IVideoThumbnailService>();
        var caller = new CallerContext(owner, ["user"], CallerType.User);

        await _service.RunFastTrackAsync(
            CreateFastJob(owner, [videoA.Id, videoB.Id]),
            _db,
            enrichmentMock.Object,
            thumbnailMock.Object,
            caller,
            CancellationToken.None);

        enrichmentMock.Verify(
            x => x.EnrichVideoAsync(videoA.Id, It.IsAny<CallerContext>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
        enrichmentMock.Verify(
            x => x.EnrichVideoAsync(videoB.Id, It.IsAny<CallerContext>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
        enrichmentMock.Verify(
            x => x.EnrichVideoAsync(videoC.Id, It.IsAny<CallerContext>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task RunFastTrackAsync_DoesNotStartScanSession()
    {
        var owner = Guid.NewGuid();
        var video = await SeedVideoAsync(owner);

        var enrichmentMock = new Mock<IVideoEnrichmentService>();
        var thumbnailMock = new Mock<IVideoThumbnailService>();
        var caller = new CallerContext(owner, ["user"], CallerType.User);

        await _service.RunFastTrackAsync(
            CreateFastJob(owner, [video.Id]),
            _db,
            enrichmentMock.Object,
            thumbnailMock.Object,
            caller,
            CancellationToken.None);

        Assert.IsFalse(_scanProgress.IsScanning(owner), "Fast-track jobs must not start a scan session.");
    }

    [TestMethod]
    public async Task RunFastTrackAsync_EmptyVideoIds_DoesNothing()
    {
        var owner = Guid.NewGuid();
        await SeedVideoAsync(owner);

        var enrichmentMock = new Mock<IVideoEnrichmentService>();
        var thumbnailMock = new Mock<IVideoThumbnailService>();
        var caller = new CallerContext(owner, ["user"], CallerType.User);

        await _service.RunFastTrackAsync(
            CreateFastJob(owner, []),
            _db,
            enrichmentMock.Object,
            thumbnailMock.Object,
            caller,
            CancellationToken.None);

        enrichmentMock.Verify(
            x => x.EnrichVideoAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
