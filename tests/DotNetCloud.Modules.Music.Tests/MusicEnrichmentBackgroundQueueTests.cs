using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class MusicEnrichmentBackgroundQueueTests
{
    [TestMethod]
    public async Task EnqueueAsync_DuplicateUserJob_ReturnsFalse()
    {
        var queue = new InMemoryMusicEnrichmentBackgroundQueue();
        var userId = Guid.NewGuid();

        var first = await queue.EnqueueAsync(new MusicEnrichmentJob
        {
            OwnerId = userId,
            FetchAlbumArt = true,
            FetchMetadata = false,
            StartedAtUtc = DateTimeOffset.UtcNow,
            TotalFiles = 10,
            TracksAdded = 5
        });

        var second = await queue.EnqueueAsync(new MusicEnrichmentJob
        {
            OwnerId = userId,
            FetchAlbumArt = true,
            FetchMetadata = false,
            StartedAtUtc = DateTimeOffset.UtcNow,
            TotalFiles = 10,
            TracksAdded = 5
        });

        Assert.IsTrue(first);
        Assert.IsFalse(second);
    }

    [TestMethod]
    public async Task BackgroundService_ReportsRemainingAlbumArtAndCompletes()
    {
        var queue = new InMemoryMusicEnrichmentBackgroundQueue();
        var progressState = new ScanProgressState();
        var userId = Guid.NewGuid();

        var progressReports = new List<LibraryScanProgress?>();
        progressState.OnProgressChanged += () =>
        {
            progressReports.Add(progressState.GetCurrentProgress(userId));
        };

        var enrichment = new Mock<IMetadataEnrichmentService>();
        enrichment
            .Setup(x => x.EnrichAlbumsWithoutArtAsync(userId, It.IsAny<IProgress<EnrichmentProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, IProgress<EnrichmentProgress>?, CancellationToken>((_, progress, _) =>
            {
                progress?.Report(new EnrichmentProgress
                {
                    Phase = "Fetching cover art...",
                    Current = 1,
                    Total = 3,
                    CurrentItem = "Album 1",
                    AlbumArtFound = 1,
                    AlbumArtRemaining = 2,
                    ArtistBiosFound = 0
                });

                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton(enrichment.Object);
        var provider = services.BuildServiceProvider();

        var backgroundService = new MusicEnrichmentBackgroundService(
            new TestScopeFactory(provider),
            queue,
            progressState,
            NullLogger<MusicEnrichmentBackgroundService>.Instance);

        var runTask = backgroundService.StartAsync(CancellationToken.None);

        var queued = await queue.EnqueueAsync(new MusicEnrichmentJob
        {
            OwnerId = userId,
            FetchAlbumArt = true,
            FetchMetadata = false,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            TotalFiles = 12,
            TracksAdded = 7,
            TracksSkipped = 2,
            TracksFailed = 1,
            TracksRemoved = 0
        });

        Assert.IsTrue(queued);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!progressReports.Any(report => report?.AlbumArtRemaining == 2) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        deadline = DateTime.UtcNow.AddSeconds(2);
        while (progressState.IsScanning(userId) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.IsFalse(progressState.IsScanning(userId));
        Assert.IsTrue(progressReports.Any(report => report?.AlbumArtRemaining == 2));

        await backgroundService.StopAsync(CancellationToken.None);
    }

    private sealed class TestScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _provider;

        public TestScopeFactory(IServiceProvider provider)
        {
            _provider = provider;
        }

        public IServiceScope CreateScope()
        {
            return new TestScope(_provider);
        }
    }

    private sealed class TestScope : IServiceScope
    {
        public TestScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }
    }
}