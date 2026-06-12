using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Photos.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public sealed class MediaFolderImportServiceTests
{
    [TestMethod]
    public async Task ScanSourcesAsync_SharedMount_EnumeratesNestedVirtualFiles()
    {
        var sharedFolderId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var firstPhotoId = Guid.CreateVersion7();
        var secondPhotoId = Guid.CreateVersion7();

        var filesApiClientMock = new Mock<IFilesApiClient>();
        filesApiClientMock
            .Setup(client => client.ScanMediaFoldersAsync(
                It.IsAny<IReadOnlyCollection<MediaLibrarySource>>(),
                ownerId,
                "Photos",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaScanCandidatesResult
            {
                Success = true,
                TotalFound = 2,
                Candidates =
                [
                    new MediaFileCandidateDto
                    {
                        Id = firstPhotoId,
                        Name = "cover.jpg",
                        Size = 128,
                        MimeType = "image/jpeg",
                        IsVirtual = true,
                    },
                    new MediaFileCandidateDto
                    {
                        Id = secondPhotoId,
                        Name = "team.png",
                        Size = 256,
                        MimeType = "image/png",
                        IsVirtual = true,
                    },
                ],
            });

        var photoCallbackMock = new Mock<IPhotoIndexingCallback>();
        photoCallbackMock
            .Setup(callback => callback.GetIndexedFileNodeIdsAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        photoCallbackMock
            .Setup(callback => callback.IndexPhotoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var provider = CreateServiceProvider(Guid.CreateVersion7().ToString(), filesApiClientMock, photoCallbackMock.Object);
        var service = CreateService(provider);

        var result = await service.ScanSourcesAsync(
            [
                new MediaLibrarySource
                {
                    SourceKind = MediaLibrarySourceKind.SharedMount,
                    SharedFolderId = sharedFolderId,
                    RelativePath = "Gallery",
                    DisplayName = "Gallery",
                    DisplayPath = "/_DotNetCloud/Gallery",
                    Enabled = true,
                }
            ],
            ownerId,
            "Photos");

        Assert.AreEqual(2, result.TotalFound);
        Assert.AreEqual(2, result.Imported);
        Assert.AreEqual(0, result.Skipped);
        Assert.AreEqual(0, result.Failed);
        Assert.AreEqual(0, result.Removed);
        Assert.AreEqual(0, result.Errors.Count);

        photoCallbackMock.Verify(
            callback => callback.IndexPhotoAsync(firstPhotoId, "cover.jpg", "image/jpeg", 128, ownerId, null, It.IsAny<CancellationToken>()),
            Times.Once);
        photoCallbackMock.Verify(
            callback => callback.IndexPhotoAsync(secondPhotoId, "team.png", "image/png", 256, ownerId, null, It.IsAny<CancellationToken>()),
            Times.Once);
        photoCallbackMock.Verify(
            callback => callback.RemoveDeletedPhotosAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ScanSourcesAsync_SharedMountUnavailable_RemovesPreviouslyIndexedFiles()
    {
        var sharedFolderId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var stalePhotoId = Guid.CreateVersion7();

        var filesApiClientMock = new Mock<IFilesApiClient>();
        filesApiClientMock
            .Setup(client => client.ScanMediaFoldersAsync(
                It.IsAny<IReadOnlyCollection<MediaLibrarySource>>(),
                ownerId,
                "Photos",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaScanCandidatesResult
            {
                Success = true,
                TotalFound = 0,
                Candidates = [],
            });

        var photoCallbackMock = new Mock<IPhotoIndexingCallback>();
        photoCallbackMock
            .Setup(callback => callback.GetIndexedFileNodeIdsAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([stalePhotoId]);
        photoCallbackMock
            .Setup(callback => callback.RemoveDeletedPhotosAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(stalePhotoId)),
                ownerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        using var provider = CreateServiceProvider(Guid.CreateVersion7().ToString(), filesApiClientMock, photoCallbackMock.Object);
        var service = CreateService(provider);

        var result = await service.ScanSourcesAsync(
            [
                new MediaLibrarySource
                {
                    SourceKind = MediaLibrarySourceKind.SharedMount,
                    SharedFolderId = sharedFolderId,
                    DisplayName = "Archive",
                    DisplayPath = "/_DotNetCloud/Archive",
                    Enabled = true,
                }
            ],
            ownerId,
            "Photos");

        Assert.AreEqual(0, result.TotalFound);
        Assert.AreEqual(0, result.Imported);
        Assert.AreEqual(0, result.Skipped);
        Assert.AreEqual(0, result.Failed);
        Assert.AreEqual(1, result.Removed);
        Assert.AreEqual(0, result.Errors.Count);

        photoCallbackMock.Verify(
            callback => callback.RemoveDeletedPhotosAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(stalePhotoId)),
                ownerId,
                It.IsAny<CancellationToken>()),
            Times.Once);
        photoCallbackMock.Verify(
            callback => callback.IndexPhotoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ServiceProvider CreateServiceProvider(string dbName, Mock<IFilesApiClient> filesApiClientMock, IPhotoIndexingCallback photoCallback)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FilesDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped(_ => filesApiClientMock.Object);
        services.AddScoped(_ => photoCallback);
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
