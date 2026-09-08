using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.SharedWithMe;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.SharedWithMe;

/// <summary>
/// Tests for <see cref="PhotosSharedWithMeProvider"/>: the Photos module adapter that feeds Files'
/// virtual "_DotNetCloud/SharedWithMe/Photos" folder.
/// </summary>
[TestClass]
public class PhotosSharedWithMeProviderTests
{
    /// <summary>
    /// Builds a provider whose DI scope resolves the given mocked module services.
    /// </summary>
    private static PhotosSharedWithMeProvider BuildProvider(
        IPhotoShareService? shareService = null,
        IAlbumService? albumService = null,
        IPhotoService? photoService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(shareService ?? Mock.Of<IPhotoShareService>());
        services.AddSingleton(albumService ?? Mock.Of<IAlbumService>());
        services.AddSingleton(photoService ?? Mock.Of<IPhotoService>());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<PhotosSharedWithMeProvider>>(
            NullLogger<PhotosSharedWithMeProvider>.Instance);
        services.AddSingleton<PhotosSharedWithMeProvider>();
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<PhotosSharedWithMeProvider>();
    }

    private static AlbumDto Album(Guid id, Guid ownerId, string title, DateTime updatedAt) => new()
    {
        Id = id,
        OwnerId = ownerId,
        Title = title,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = updatedAt
    };

    private static PhotoDto Photo(Guid id, Guid ownerId, string fileName, DateTime updatedAt) => new()
    {
        Id = id,
        FileNodeId = Guid.CreateVersion7(),
        OwnerId = ownerId,
        FileName = fileName,
        MimeType = "image/jpeg",
        TakenAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = updatedAt
    };

    private static PhotoShareDto AlbumShare(Guid albumId) => new()
    {
        Id = Guid.CreateVersion7(),
        AlbumId = albumId,
        SharedWithUserId = Guid.CreateVersion7(),
        Permission = PhotoSharePermission.ReadOnly,
        CreatedAt = DateTime.UtcNow
    };

    private static PhotoShareDto PhotoShare(Guid photoId) => new()
    {
        Id = Guid.CreateVersion7(),
        PhotoId = photoId,
        SharedWithUserId = Guid.CreateVersion7(),
        Permission = PhotoSharePermission.ReadOnly,
        CreatedAt = DateTime.UtcNow
    };

    [TestMethod]
    public async Task ListAsync_AlbumShare_ReturnsAlbumItemWithTitleAndAlbumDeepLink()
    {
        var userId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var albumId = Guid.CreateVersion7();
        var updatedAt = DateTime.UtcNow.AddHours(-1);

        var shareService = new Mock<IPhotoShareService>();
        shareService
            .Setup(s => s.GetSharedWithMeAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AlbumShare(albumId)]);

        var albumService = new Mock<IAlbumService>();
        albumService
            .Setup(s => s.GetAlbumAsync(albumId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Album(albumId, ownerId, "Beach Trip", updatedAt));

        var provider = BuildProvider(shareService.Object, albumService.Object);
        var items = await provider.ListAsync(userId);

        Assert.AreEqual(1, items.Count);
        var item = items.Single();
        Assert.AreEqual("photos", item.ModuleId);
        Assert.AreEqual("Photos", item.DisplayName);
        Assert.AreEqual("Album", item.EntityType);
        Assert.AreEqual(albumId, item.EntityId);
        Assert.AreEqual("Beach Trip", item.Title);
        Assert.AreEqual($"/apps/photos?albumId={albumId}", item.DeepLink);
        Assert.AreEqual("photo_library", item.IconName);
        Assert.AreEqual(updatedAt, item.UpdatedAt);
        Assert.AreEqual(1, await provider.CountAsync(userId));
    }

    [TestMethod]
    public async Task ListAsync_PhotoShare_ReturnsPhotoItemFromFileNameWithPhotoDeepLink()
    {
        var userId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var photoId = Guid.CreateVersion7();
        var updatedAt = DateTime.UtcNow.AddHours(-2);

        var shareService = new Mock<IPhotoShareService>();
        shareService
            .Setup(s => s.GetSharedWithMeAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PhotoShare(photoId)]);

        var photoService = new Mock<IPhotoService>();
        photoService
            .Setup(s => s.GetPhotoAsync(photoId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Photo(photoId, ownerId, "IMG_0001.jpg", updatedAt));

        var provider = BuildProvider(shareService.Object, photoService: photoService.Object);
        var items = await provider.ListAsync(userId);

        Assert.AreEqual(1, items.Count);
        var item = items.Single();
        Assert.AreEqual("photos", item.ModuleId);
        Assert.AreEqual("Photo", item.EntityType);
        Assert.AreEqual(photoId, item.EntityId);
        Assert.AreEqual("IMG_0001.jpg", item.Title);
        Assert.AreEqual($"/apps/photos?photoId={photoId}", item.DeepLink);
        Assert.AreEqual("image", item.IconName);
        Assert.AreEqual(updatedAt, item.UpdatedAt);
    }

    [TestMethod]
    public async Task ListAsync_DuplicateSharesOfSameEntity_DeduplicatesAndSkipsUnresolvable()
    {
        var userId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var albumId = Guid.CreateVersion7();
        var missingAlbumId = Guid.CreateVersion7();

        var shareService = new Mock<IPhotoShareService>();
        shareService
            .Setup(s => s.GetSharedWithMeAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                AlbumShare(albumId),
                new PhotoShareDto
                {
                    Id = Guid.CreateVersion7(),
                    AlbumId = albumId,
                    SharedWithTeamId = Guid.CreateVersion7(),
                    Permission = PhotoSharePermission.ReadOnly,
                    CreatedAt = DateTime.UtcNow
                },
                AlbumShare(missingAlbumId)
            ]);

        var albumService = new Mock<IAlbumService>();
        albumService
            .Setup(s => s.GetAlbumAsync(albumId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Album(albumId, ownerId, "Beach Trip", DateTime.UtcNow));
        albumService
            .Setup(s => s.GetAlbumAsync(missingAlbumId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlbumDto?)null);

        var provider = BuildProvider(shareService.Object, albumService.Object);
        var items = await provider.ListAsync(userId);

        // Only the resolvable album appears, and only once despite two shares.
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(albumId, items.Single().EntityId);
    }

    [TestMethod]
    public async Task ListAsync_NoShares_ReturnsEmpty()
    {
        var shareService = new Mock<IPhotoShareService>();
        shareService
            .Setup(s => s.GetSharedWithMeAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var provider = BuildProvider(shareService.Object);
        var items = await provider.ListAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, items.Count);
        Assert.AreEqual(0, await provider.CountAsync(Guid.CreateVersion7()));
    }

    [TestMethod]
    public async Task ListAsync_ShareServiceThrows_ReturnsEmpty()
    {
        var shareService = new Mock<IPhotoShareService>();
        shareService
            .Setup(s => s.GetSharedWithMeAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Photos module unavailable"));

        var provider = BuildProvider(shareService.Object);
        var items = await provider.ListAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, items.Count);
        Assert.AreEqual(0, await provider.CountAsync(Guid.CreateVersion7()));
    }
}
