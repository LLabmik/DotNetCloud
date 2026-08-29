using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class QuickVideoEnrichmentServiceTests
{
    private VideoDbContext _db = null!;
    private Mock<IVideoEnrichmentBackgroundQueue> _queueMock = null!;
    private QuickVideoEnrichmentService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _queueMock = new Mock<IVideoEnrichmentBackgroundQueue>();
        _queueMock
            .Setup(q => q.EnqueueAsync(It.IsAny<VideoEnrichmentJob>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true));

        // Real scope factory so ProcessPendingBurstsAsync resolves the DbContext
        // through the same path it would in the module host.
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        var provider = services.BuildServiceProvider();

        _service = new QuickVideoEnrichmentService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            _queueMock.Object,
            NullLogger<QuickVideoEnrichmentService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task<UserVideo> SeedVideoAsync(Guid ownerId, DateTime createdAtUtc, int? tmdbId = null)
    {
        var contentHash = Guid.CreateVersion7().ToString("N");
        var canonical = new CanonicalVideo
        {
            ContentHash = contentHash,
            Title = $"Video {contentHash[..8]}",
            FileName = $"video-{contentHash[..8]}.mp4",
            MimeType = "video/mp4",
            SizeBytes = 1024,
            TmdbId = tmdbId
        };
        _db.CanonicalVideos.Add(canonical);

        var userVideo = new UserVideo
        {
            OwnerId = ownerId,
            FileNodeId = Guid.CreateVersion7(),
            CanonicalContentHash = contentHash,
            CreatedAt = createdAtUtc,
            UpdatedAt = createdAtUtc
        };
        _db.UserVideos.Add(userVideo);
        await _db.SaveChangesAsync();

        return userVideo;
    }

    private VideoEnrichmentJob? CaptureEnqueuedJob()
    {
        var invocation = _queueMock.Invocations
            .FirstOrDefault(i => i.Method.Name == nameof(IVideoEnrichmentBackgroundQueue.EnqueueAsync));
        return invocation?.Arguments[0] as VideoEnrichmentJob;
    }

    private static DateTime Quiet() => DateTime.UtcNow - QuickVideoEnrichmentPolicy.QuietPeriod - TimeSpan.FromSeconds(30);

    private static DateTime NotQuiet() => DateTime.UtcNow - TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task ProcessPendingBurstsAsync_SmallQuietBurst_EnqueuesScopedFastJob()
    {
        var owner = Guid.NewGuid();
        await SeedVideoAsync(owner, Quiet());
        await SeedVideoAsync(owner, Quiet());
        await SeedVideoAsync(owner, Quiet());

        await _service.ProcessPendingBurstsAsync();

        _queueMock.Verify(q => q.EnqueueAsync(It.IsAny<VideoEnrichmentJob>(), It.IsAny<CancellationToken>()), Times.Once);
        var job = CaptureEnqueuedJob();
        Assert.IsNotNull(job);
        Assert.AreEqual(owner, job!.OwnerId);
        Assert.IsTrue(job.IsFastTrack);
        Assert.IsNotNull(job.VideoIds);
        Assert.AreEqual(3, job.VideoIds!.Count);
    }

    [TestMethod]
    public async Task ProcessPendingBurstsAsync_BurstOverThreshold_DoesNotEnqueue()
    {
        var owner = Guid.NewGuid();
        for (var i = 0; i < QuickVideoEnrichmentPolicy.MaxQuickBatchSize + 1; i++)
            await SeedVideoAsync(owner, Quiet());

        await _service.ProcessPendingBurstsAsync();

        _queueMock.Verify(q => q.EnqueueAsync(It.IsAny<VideoEnrichmentJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessPendingBurstsAsync_BurstNotQuiet_DoesNotEnqueue()
    {
        var owner = Guid.NewGuid();
        await SeedVideoAsync(owner, NotQuiet());
        await SeedVideoAsync(owner, NotQuiet());

        await _service.ProcessPendingBurstsAsync();

        _queueMock.Verify(q => q.EnqueueAsync(It.IsAny<VideoEnrichmentJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessPendingBurstsAsync_AlreadyEnriched_Skips()
    {
        var owner = Guid.NewGuid();
        await SeedVideoAsync(owner, Quiet(), tmdbId: 123);
        await SeedVideoAsync(owner, Quiet(), tmdbId: 456);

        await _service.ProcessPendingBurstsAsync();

        _queueMock.Verify(q => q.EnqueueAsync(It.IsAny<VideoEnrichmentJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessPendingBurstsAsync_OldVideosOutsideLookback_Skips()
    {
        var owner = Guid.NewGuid();
        var old = DateTime.UtcNow - QuickVideoEnrichmentPolicy.LookbackWindow - TimeSpan.FromMinutes(1);
        await SeedVideoAsync(owner, old);
        await SeedVideoAsync(owner, old);

        await _service.ProcessPendingBurstsAsync();

        _queueMock.Verify(q => q.EnqueueAsync(It.IsAny<VideoEnrichmentJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessPendingBurstsAsync_AttemptedVideos_NotReenqueued()
    {
        var owner = Guid.NewGuid();
        await SeedVideoAsync(owner, Quiet());

        await _service.ProcessPendingBurstsAsync();
        await _service.ProcessPendingBurstsAsync();

        _queueMock.Verify(q => q.EnqueueAsync(It.IsAny<VideoEnrichmentJob>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessPendingBurstsAsync_MultipleOwners_Independent()
    {
        var ownerSmall = Guid.NewGuid();
        var ownerLarge = Guid.NewGuid();
        await SeedVideoAsync(ownerSmall, Quiet());
        await SeedVideoAsync(ownerSmall, Quiet());
        for (var i = 0; i < QuickVideoEnrichmentPolicy.MaxQuickBatchSize + 1; i++)
            await SeedVideoAsync(ownerLarge, Quiet());

        await _service.ProcessPendingBurstsAsync();

        _queueMock.Verify(q => q.EnqueueAsync(It.IsAny<VideoEnrichmentJob>(), It.IsAny<CancellationToken>()), Times.Once);
        var job = CaptureEnqueuedJob();
        Assert.IsNotNull(job);
        Assert.AreEqual(ownerSmall, job!.OwnerId);
        Assert.AreEqual(2, job.VideoIds!.Count);
    }

    [TestMethod]
    public async Task ProcessPendingBurstsAsync_QueueRejectsJob_DoesNotMarkAttempted()
    {
        var owner = Guid.NewGuid();
        await SeedVideoAsync(owner, Quiet());
        _queueMock
            .Setup(q => q.EnqueueAsync(It.IsAny<VideoEnrichmentJob>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(false));

        await _service.ProcessPendingBurstsAsync();
        await _service.ProcessPendingBurstsAsync();

        _queueMock.Verify(q => q.EnqueueAsync(It.IsAny<VideoEnrichmentJob>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
