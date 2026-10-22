using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Covers the background-queue hand-off: indexing must stay insert-only and only fall back to
/// inline generation when no queue is available (or the bounded queue is full).
/// </summary>
[TestClass]
public sealed class PhotoIndexingCallbackQueueTests
{
    private PhotosDbContext _db = null!;
    private PhotoService _photoService = null!;
    private Mock<IPhotoThumbnailService> _thumbnailMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _photoService = new PhotoService(
            _db,
            Mock.Of<IEventBus>(),
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            Mock.Of<ILogger<PhotoService>>());
        _thumbnailMock = new Mock<IPhotoThumbnailService>();
        _thumbnailMock
            .Setup(thumbnails => thumbnails.GenerateThumbnailsAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task IndexPhotoAsync_WithQueue_EnqueuesJobInsteadOfGeneratingInline()
    {
        var queue = new PhotoThumbnailQueue(8);
        var callback = CreateCallback(queue);
        var fileNodeId = Guid.CreateVersion7();

        await callback.IndexPhotoAsync(fileNodeId, "sunset.jpg", "image/jpeg", 1024, Guid.CreateVersion7(), "files/aa/bb");

        _thumbnailMock.Verify(
            thumbnails => thumbnails.GenerateThumbnailsAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.AreEqual(1, queue.PendingCount);
        var item = await queue.DequeueAsync(CancellationToken.None);
        var photo = _db.Photos.Single(p => p.FileNodeId == fileNodeId);
        Assert.AreEqual(photo.Id, item.PhotoId);
        Assert.AreEqual("files/aa/bb", item.StoragePath);
        Assert.AreEqual("image/jpeg", item.MimeType);
    }

    [TestMethod]
    public async Task IndexPhotoAsync_WithoutQueue_GeneratesInline()
    {
        var callback = CreateCallback(queue: null);
        var fileNodeId = Guid.CreateVersion7();

        await callback.IndexPhotoAsync(fileNodeId, "sunset.jpg", "image/jpeg", 1024, Guid.CreateVersion7());

        var photo = _db.Photos.Single(p => p.FileNodeId == fileNodeId);
        _thumbnailMock.Verify(
            thumbnails => thumbnails.GenerateThumbnailsAsync(
                photo.Id, null, "image/jpeg", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task IndexPhotoAsync_QueueFull_FallsBackToInlineGeneration()
    {
        var queue = new PhotoThumbnailQueue(1);
        Assert.IsTrue(queue.TryEnqueue(new PhotoThumbnailWorkItem(Guid.CreateVersion7(), null, "image/jpeg")));

        var callback = CreateCallback(queue);
        var fileNodeId = Guid.CreateVersion7();

        await callback.IndexPhotoAsync(fileNodeId, "sunset.jpg", "image/jpeg", 1024, Guid.CreateVersion7());

        var photo = _db.Photos.Single(p => p.FileNodeId == fileNodeId);
        _thumbnailMock.Verify(
            thumbnails => thumbnails.GenerateThumbnailsAsync(
                photo.Id, null, "image/jpeg", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.AreEqual(1, queue.PendingCount);
    }

    [TestMethod]
    public async Task IndexPhotoAsync_ExistingPhoto_DoesNotCreateDuplicateRow()
    {
        var queue = new PhotoThumbnailQueue(8);
        var callback = CreateCallback(queue);
        var fileNodeId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();

        await callback.IndexPhotoAsync(fileNodeId, "sunset.jpg", "image/jpeg", 1024, ownerId);
        await callback.IndexPhotoAsync(fileNodeId, "sunset.jpg", "image/jpeg", 1024, ownerId);

        Assert.AreEqual(1, _db.Photos.Count(p => p.FileNodeId == fileNodeId));
        // Both index calls hand off thumbnail work; the thumbnail service itself skips
        // photos that already have both sizes.
        Assert.AreEqual(2, queue.PendingCount);
    }

    private PhotoIndexingCallback CreateCallback(IPhotoThumbnailQueue? queue)
        => new(_photoService, _thumbnailMock.Object, _db, Mock.Of<ILogger<PhotoIndexingCallback>>(), queue);
}
