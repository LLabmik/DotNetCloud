using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class PhotoIndexingCallbackTests
{
    private PhotosDbContext _db = null!;
    private PhotoService _photoService = null!;
    private PhotoIndexingCallback _callback = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _photoService = new PhotoService(_db, Mock.Of<IEventBus>(), Mock.Of<ILogger<PhotoService>>());
        _callback = new PhotoIndexingCallback(_photoService, Mock.Of<IPhotoThumbnailService>(), Mock.Of<ILogger<PhotoIndexingCallback>>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task IndexPhotoAsync_CreatesPhotoInDatabase()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _callback.IndexPhotoAsync(fileNodeId, "sunset.jpg", "image/jpeg", 2_000_000, ownerId);

        var count = _db.Photos.Count(p => p.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task IndexPhotoAsync_SetsCorrectOwner()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _callback.IndexPhotoAsync(fileNodeId, "photo.png", "image/png", 1024, ownerId);

        var photo = _db.Photos.First(p => p.FileNodeId == fileNodeId);
        Assert.AreEqual(ownerId, photo.OwnerId);
    }

    [TestMethod]
    public async Task IndexPhotoAsync_SetsCorrectFileName()
    {
        var fileNodeId = Guid.NewGuid();

        await _callback.IndexPhotoAsync(fileNodeId, "vacation.heic", "image/heic", 5_000_000, Guid.NewGuid());

        var photo = _db.Photos.First(p => p.FileNodeId == fileNodeId);
        Assert.AreEqual("vacation.heic", photo.FileName);
    }

    [TestMethod]
    public async Task IndexPhotoAsync_SetsCorrectMimeType()
    {
        var fileNodeId = Guid.NewGuid();

        await _callback.IndexPhotoAsync(fileNodeId, "photo.webp", "image/webp", 1024, Guid.NewGuid());

        var photo = _db.Photos.First(p => p.FileNodeId == fileNodeId);
        Assert.AreEqual("image/webp", photo.MimeType);
    }

    [TestMethod]
    public async Task IndexPhotoAsync_SetsCorrectSizeBytes()
    {
        var fileNodeId = Guid.NewGuid();

        await _callback.IndexPhotoAsync(fileNodeId, "large.jpg", "image/jpeg", 15_000_000, Guid.NewGuid());

        var photo = _db.Photos.First(p => p.FileNodeId == fileNodeId);
        Assert.AreEqual(15_000_000, photo.SizeBytes);
    }

    [TestMethod]
    public async Task IndexPhotoAsync_MultipleFiles_CreatesAll()
    {
        var ownerId = Guid.NewGuid();

        await _callback.IndexPhotoAsync(Guid.NewGuid(), "a.jpg", "image/jpeg", 1024, ownerId);
        await _callback.IndexPhotoAsync(Guid.NewGuid(), "b.png", "image/png", 2048, ownerId);
        await _callback.IndexPhotoAsync(Guid.NewGuid(), "c.gif", "image/gif", 512, ownerId);

        Assert.AreEqual(3, _db.Photos.Count());
    }

    [TestMethod]
    public async Task IndexPhotoAsync_DuplicateFileNodeId_SkipsInsert()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _callback.IndexPhotoAsync(fileNodeId, "sunset.jpg", "image/jpeg", 2_000_000, ownerId);
        await _callback.IndexPhotoAsync(fileNodeId, "sunset.jpg", "image/jpeg", 2_000_000, ownerId);

        Assert.AreEqual(1, _db.Photos.Count(p => p.FileNodeId == fileNodeId));
    }
}
