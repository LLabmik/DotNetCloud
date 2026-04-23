using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Photos.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        var sharedFolderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var rootFolderId = Guid.NewGuid();
        var nestedFolderId = Guid.NewGuid();
        var firstPhotoId = Guid.NewGuid();
        var secondPhotoId = Guid.NewGuid();

        var rootFolder = CreateFolder(rootFolderId, "Gallery");
        var nestedFolder = CreateFolder(nestedFolderId, "Events", rootFolderId);
        var firstPhoto = CreateFile(firstPhotoId, "cover.jpg", "image/jpeg", 128, rootFolderId);
        var secondPhoto = CreateFile(secondPhotoId, "team.png", "image/png", 256, nestedFolderId);
        var note = CreateFile(Guid.NewGuid(), "notes.txt", "text/plain", 64, rootFolderId);

        var fileServiceMock = new Mock<IFileService>();
        fileServiceMock
            .Setup(service => service.ResolveMountedNodeAsync(sharedFolderId, "Gallery", It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rootFolder);
        fileServiceMock
            .Setup(service => service.ListChildrenAsync(rootFolderId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstPhoto, note, nestedFolder]);
        fileServiceMock
            .Setup(service => service.ListChildrenAsync(nestedFolderId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([secondPhoto]);

        var photoCallbackMock = new Mock<IPhotoIndexingCallback>();
        photoCallbackMock
            .Setup(callback => callback.GetIndexedFileNodeIdsAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        photoCallbackMock
            .Setup(callback => callback.IndexPhotoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var provider = CreateServiceProvider(Guid.NewGuid().ToString(), fileServiceMock.Object, photoCallbackMock.Object);
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

        fileServiceMock.Verify(
            service => service.GetStoragePathAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ScanSourcesAsync_SharedMountUnavailable_RemovesPreviouslyIndexedFiles()
    {
        var sharedFolderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var stalePhotoId = Guid.NewGuid();

        var fileServiceMock = new Mock<IFileService>();
        fileServiceMock
            .Setup(service => service.ResolveMountedNodeAsync(sharedFolderId, null, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileNodeDto?)null);

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

        using var provider = CreateServiceProvider(Guid.NewGuid().ToString(), fileServiceMock.Object, photoCallbackMock.Object);
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
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "/_DotNetCloud/Archive");

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

    private static ServiceProvider CreateServiceProvider(string dbName, IFileService fileService, IPhotoIndexingCallback photoCallback)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FilesDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped(_ => fileService);
        services.AddScoped(_ => photoCallback);
        return services.BuildServiceProvider();
    }

    private static MediaFolderImportService CreateService(ServiceProvider provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Files:Storage:RootPath"] = Path.GetTempPath(),
                })
            .Build();

        return new MediaFolderImportService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<IFileStorageEngine>(),
            Mock.Of<IEventBus>(),
            configuration,
            NullLogger<MediaFolderImportService>.Instance);
    }

    private static FileNodeDto CreateFolder(Guid id, string name, Guid? parentId = null)
        => new()
        {
            Id = id,
            Name = name,
            NodeType = "Folder",
            ParentId = parentId,
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsVirtual = true,
            IsReadOnly = true,
        };

    private static FileNodeDto CreateFile(Guid id, string name, string mimeType, long size, Guid? parentId)
        => new()
        {
            Id = id,
            Name = name,
            NodeType = "File",
            MimeType = mimeType,
            Size = size,
            ParentId = parentId,
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsVirtual = true,
            IsReadOnly = true,
        };
}