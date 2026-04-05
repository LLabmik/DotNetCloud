using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Events;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class PhotoShareServiceTests
{
    private PhotosDbContext _db;
    private PhotoShareService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _service = new PhotoShareService(_db, _eventBusMock.Object, NullLogger<PhotoShareService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── SharePhoto ───────────────────────────────────────────────────

    [TestMethod]
    public async Task SharePhoto_Valid_CreatesShare()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var targetUserId = Guid.NewGuid();

        var share = await _service.SharePhotoAsync(photo.Id, targetUserId, PhotoSharePermission.ReadOnly, _caller);

        Assert.IsNotNull(share);
        Assert.AreEqual(photo.Id, share.PhotoId);
        Assert.AreEqual(targetUserId, share.SharedWithUserId);
        Assert.AreEqual(PhotoSharePermission.ReadOnly, share.Permission);
    }

    [TestMethod]
    public async Task SharePhoto_NonExistentPhoto_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.SharePhotoAsync(Guid.NewGuid(), Guid.NewGuid(), PhotoSharePermission.ReadOnly, _caller));
    }

    [TestMethod]
    public async Task SharePhoto_DownloadPermission_AllowsDownload()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var targetUserId = Guid.NewGuid();

        var share = await _service.SharePhotoAsync(photo.Id, targetUserId, PhotoSharePermission.Download, _caller);

        Assert.AreEqual(PhotoSharePermission.Download, share.Permission);
    }

    // ─── ShareAlbum ───────────────────────────────────────────────────

    [TestMethod]
    public async Task ShareAlbum_Valid_CreatesShareAndPublishesEvent()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);
        var targetUserId = Guid.NewGuid();

        var share = await _service.ShareAlbumAsync(album.Id, targetUserId, PhotoSharePermission.Contribute, _caller);

        Assert.IsNotNull(share);
        Assert.AreEqual(album.Id, share.AlbumId);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<AlbumSharedEvent>(e => e.AlbumId == album.Id),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ShareAlbum_NonExistentAlbum_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ShareAlbumAsync(Guid.NewGuid(), Guid.NewGuid(), PhotoSharePermission.ReadOnly, _caller));
    }

    // ─── RemoveShare ──────────────────────────────────────────────────

    [TestMethod]
    public async Task RemoveShare_Valid_RemovesShare()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        var share = await TestHelpers.SeedPhotoShareAsync(_db, photo.Id, _caller.UserId, Guid.NewGuid());

        await _service.RemoveShareAsync(share.Id, _caller);

        Assert.AreEqual(0, _db.PhotoShares.Count());
    }

    [TestMethod]
    public async Task RemoveShare_NonExistent_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.RemoveShareAsync(Guid.NewGuid(), _caller));
    }

    // ─── GetPhotoShares ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetPhotoShares_ReturnsSharesForPhoto()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        await TestHelpers.SeedPhotoShareAsync(_db, photo.Id, _caller.UserId, Guid.NewGuid());
        await TestHelpers.SeedPhotoShareAsync(_db, photo.Id, _caller.UserId, Guid.NewGuid());

        var shares = await _service.GetPhotoSharesAsync(photo.Id, _caller);

        Assert.AreEqual(2, shares.Count);
    }

    [TestMethod]
    public async Task GetPhotoShares_NoShares_ReturnsEmpty()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var shares = await _service.GetPhotoSharesAsync(photo.Id, _caller);

        Assert.AreEqual(0, shares.Count);
    }

    // ─── GetSharedWithMe ──────────────────────────────────────────────

    [TestMethod]
    public async Task GetSharedWithMe_ReturnsSharesTargetingCaller()
    {
        var ownerId = Guid.NewGuid();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);
        await TestHelpers.SeedPhotoShareAsync(_db, photo.Id, ownerId, _caller.UserId);

        var shares = await _service.GetSharedWithMeAsync(_caller);

        Assert.AreEqual(1, shares.Count);
    }

    [TestMethod]
    public async Task GetSharedWithMe_NoShares_ReturnsEmpty()
    {
        var shares = await _service.GetSharedWithMeAsync(_caller);
        Assert.AreEqual(0, shares.Count);
    }

    // ─── GetAlbumShares ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetAlbumShares_ReturnsSharesForAlbum()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);
        var share = new PhotoShare
        {
            AlbumId = album.Id,
            SharedByUserId = _caller.UserId,
            SharedWithUserId = Guid.NewGuid(),
            Permission = PhotoSharePermissionLevel.ReadOnly
        };
        _db.PhotoShares.Add(share);
        await _db.SaveChangesAsync();

        var shares = await _service.GetAlbumSharesAsync(album.Id, _caller);

        Assert.AreEqual(1, shares.Count);
    }
}
