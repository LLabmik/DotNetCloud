using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Services;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Photos.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Covers the concurrent indexing path: photos are indexed by several workers at once,
/// failures are isolated per file, cancellation stops promptly and progress is still reported.
/// </summary>
[TestClass]
public sealed class MediaFolderImportServiceParallelTests
{
    [TestMethod]
    public async Task ScanSourcesAsync_ManyPhotos_IndexesThemConcurrently()
    {
        var ownerId = Guid.CreateVersion7();
        var candidates = Enumerable.Range(0, 12).Select(i => Candidate($"photo{i:00}.jpg")).ToList();

        var active = 0;
        var maxActive = 0;
        var callback = new Mock<IPhotoIndexingCallback>();
        callback.Setup(c => c.GetIndexedFileNodeIdsAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        callback.Setup(c => c.IndexPhotoAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, string _, string _, long _, Guid _, string? _, CancellationToken ct) =>
            {
                var current = Interlocked.Increment(ref active);
                InterlockedMax(ref maxActive, current);
                try
                {
                    await Task.Delay(30, ct);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            });

        using var provider = CreateProvider(callback, candidates, ownerId);
        var service = CreateService(provider);

        var result = await service.ScanSourcesAsync(Sources(), ownerId, "Photos");

        Assert.AreEqual(12, result.TotalFound);
        Assert.AreEqual(12, result.Imported);
        Assert.AreEqual(0, result.Failed);
        Assert.IsTrue(maxActive > 1, $"Expected concurrent indexing, max concurrency was {maxActive}.");
    }

    [TestMethod]
    public async Task ScanSourcesAsync_OneFileFails_OtherFilesStillIndexed()
    {
        var ownerId = Guid.CreateVersion7();
        var candidates = Enumerable.Range(0, 6).Select(i => Candidate($"photo{i:00}.jpg")).ToList();
        var failingId = candidates[2].Id;

        var callback = new Mock<IPhotoIndexingCallback>();
        callback.Setup(c => c.GetIndexedFileNodeIdsAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        callback.Setup(c => c.IndexPhotoAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid fileNodeId, string _, string _, long _, Guid _, string? _, CancellationToken _) =>
                fileNodeId == failingId
                    ? Task.FromException(new InvalidOperationException("decode failed"))
                    : Task.CompletedTask);

        using var provider = CreateProvider(callback, candidates, ownerId);
        var service = CreateService(provider);

        var result = await service.ScanSourcesAsync(Sources(), ownerId, "Photos");

        Assert.AreEqual(6, result.TotalFound);
        Assert.AreEqual(5, result.Imported);
        Assert.AreEqual(1, result.Failed);
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "decode failed");
    }

    [TestMethod]
    public async Task ScanSourcesAsync_Cancelled_StopsWithoutIndexing()
    {
        var ownerId = Guid.CreateVersion7();
        var candidates = Enumerable.Range(0, 5).Select(i => Candidate($"photo{i:00}.jpg")).ToList();

        var callback = new Mock<IPhotoIndexingCallback>();
        callback.Setup(c => c.GetIndexedFileNodeIdsAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var provider = CreateProvider(callback, candidates, ownerId);
        var service = CreateService(provider);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.ScanSourcesAsync(Sources(), ownerId, "Photos", progress: null, cts.Token);

        Assert.AreEqual(0, result.Imported);
        callback.Verify(
            c => c.IndexPhotoAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ScanSourcesAsync_ReportsFinalProgressForTheWholeBatch()
    {
        var ownerId = Guid.CreateVersion7();
        var candidates = Enumerable.Range(0, 4).Select(i => Candidate($"photo{i:00}.jpg")).ToList();

        var callback = new Mock<IPhotoIndexingCallback>();
        callback.Setup(c => c.GetIndexedFileNodeIdsAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        callback.Setup(c => c.IndexPhotoAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var provider = CreateProvider(callback, candidates, ownerId);
        var service = CreateService(provider);

        var snapshots = new List<MediaScanProgress>();
        var progress = new Progress<MediaScanProgress>(snapshots.Add);

        var result = await service.ScanSourcesAsync(Sources(), ownerId, "Photos", progress);

        Assert.AreEqual(4, result.Imported);

        // Progress<T> dispatches asynchronously — wait (bounded) for the final snapshot.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((snapshots.Count == 0 || snapshots.Count < 1 || snapshots[^1].FilesProcessed < 4) &&
               DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsTrue(snapshots.Count > 0, "Expected at least one progress report.");
        var last = snapshots[^1];
        Assert.AreEqual(4, last.FilesProcessed);
        Assert.AreEqual(4, last.TotalFiles);
        Assert.AreEqual(100, last.PercentComplete);
        Assert.AreEqual(4, last.Imported);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static void InterlockedMax(ref int target, int candidate)
    {
        var current = Volatile.Read(ref target);
        while (candidate > current)
        {
            var previous = Interlocked.CompareExchange(ref target, candidate, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    private static MediaFileCandidateDto Candidate(string name) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Size = 1024,
        MimeType = "image/jpeg",
    };

    private static List<MediaLibrarySource> Sources() =>
    [
        new MediaLibrarySource
        {
            SourceKind = MediaLibrarySourceKind.OwnedFileNode,
            FolderId = Guid.CreateVersion7(),
            DisplayPath = "/",
            DisplayName = "Home",
            Enabled = true,
        }
    ];

    private static ServiceProvider CreateProvider(
        Mock<IPhotoIndexingCallback> callback,
        IReadOnlyList<MediaFileCandidateDto> candidates,
        Guid ownerId)
    {
        var filesApiClient = new Mock<IFilesApiClient>();
        filesApiClient
            .Setup(client => client.ScanMediaFoldersAsync(
                It.IsAny<IReadOnlyCollection<MediaLibrarySource>>(),
                ownerId,
                "Photos",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaScanCandidatesResult
            {
                Success = true,
                TotalFound = candidates.Count,
                Candidates = candidates,
            });

        var services = new ServiceCollection();
        services.AddDbContext<FilesDbContext>(options => options.UseInMemoryDatabase(Guid.CreateVersion7().ToString()));
        services.AddScoped(_ => filesApiClient.Object);
        services.AddScoped(_ => callback.Object);
        return services.BuildServiceProvider();
    }

    private static MediaFolderImportService CreateService(ServiceProvider provider)
    {
        return new MediaFolderImportService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IFilesApiClient>(),
            NullLogger<MediaFolderImportService>.Instance);
    }
}
