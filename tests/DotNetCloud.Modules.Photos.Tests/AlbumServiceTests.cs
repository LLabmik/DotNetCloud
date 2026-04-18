using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class AlbumServiceTests
{
    private PhotosDbContext _db;
    private AlbumService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _service = new AlbumService(_db, _eventBusMock.Object, NullLogger<AlbumService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAlbum_ValidDto_ReturnsAlbumDto()
    {
        var dto = new CreateAlbumDto { Title = "Vacation 2025", Description = "Summer trip" };

        var result = await _service.CreateAlbumAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Vacation 2025", result.Title);
        Assert.AreEqual("Summer trip", result.Description);
        Assert.AreEqual(_caller.UserId, result.OwnerId);
    }

    [TestMethod]
    public async Task CreateAlbum_PublishesAlbumCreatedEvent()
    {
        var dto = new CreateAlbumDto { Title = "Events Album" };

        await _service.CreateAlbumAsync(dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<AlbumCreatedEvent>(e => e.Title == "Events Album"),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreateAlbum_PersistedToDatabase()
    {
        var dto = new CreateAlbumDto { Title = "DB Album" };

        await _service.CreateAlbumAsync(dto, _caller);

        Assert.AreEqual(1, _db.Albums.Count());
    }

    // ─── Get ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetAlbum_AsOwner_ReturnsAlbum()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);

        var result = await _service.GetAlbumAsync(album.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(album.Title, result.Title);
    }

    [TestMethod]
    public async Task GetAlbum_NonOwner_ReturnsNull()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, Guid.NewGuid());

        var result = await _service.GetAlbumAsync(album.Id, _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAlbum_NonExistent_ReturnsNull()
    {
        var result = await _service.GetAlbumAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    // ─── List ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListAlbums_ReturnsOnlyOwnAlbums()
    {
        await TestHelpers.SeedAlbumAsync(_db, _caller.UserId, "Mine");
        await TestHelpers.SeedAlbumAsync(_db, Guid.NewGuid(), "Theirs");

        var result = await _service.ListAlbumsAsync(_caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Mine", result[0].Title);
    }

    [TestMethod]
    public async Task ListAlbums_EmptyForNewUser_ReturnsEmpty()
    {
        var result = await _service.ListAlbumsAsync(_caller);
        Assert.AreEqual(0, result.Count);
    }

    // ─── Update ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateAlbum_ChangesTitle()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId, "Old Title");
        var dto = new UpdateAlbumDto { Title = "New Title" };

        var result = await _service.UpdateAlbumAsync(album.Id, dto, _caller);

        Assert.AreEqual("New Title", result.Title);
    }

    [TestMethod]
    public async Task UpdateAlbum_NonExistent_ThrowsBusinessRuleException()
    {
        var dto = new UpdateAlbumDto { Title = "X" };

        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.UpdateAlbumAsync(Guid.NewGuid(), dto, _caller));
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAlbum_SoftDeletes()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);

        await _service.DeleteAlbumAsync(album.Id, _caller);

        Assert.AreEqual(0, _db.Albums.Count());
        Assert.AreEqual(1, _db.Albums.IgnoreQueryFilters().Count());
    }

    [TestMethod]
    public async Task DeleteAlbum_NonExistent_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.DeleteAlbumAsync(Guid.NewGuid(), _caller));
    }

    // ─── AddPhoto / RemovePhoto ───────────────────────────────────────

    [TestMethod]
    public async Task AddPhotoToAlbum_AddsSuccessfully()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        await _service.AddPhotoToAlbumAsync(album.Id, photo.Id, _caller);

        Assert.AreEqual(1, _db.AlbumPhotos.Count());
    }

    [TestMethod]
    public async Task AddPhotoToAlbum_Duplicate_ThrowsBusinessRuleException()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        await _service.AddPhotoToAlbumAsync(album.Id, photo.Id, _caller);

        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.AddPhotoToAlbumAsync(album.Id, photo.Id, _caller));
    }

    [TestMethod]
    public async Task RemovePhotoFromAlbum_RemovesSuccessfully()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        await _service.AddPhotoToAlbumAsync(album.Id, photo.Id, _caller);

        await _service.RemovePhotoFromAlbumAsync(album.Id, photo.Id, _caller);

        Assert.AreEqual(0, _db.AlbumPhotos.Count());
    }

    [TestMethod]
    public async Task AddPhotoToAlbum_NonExistentAlbum_ThrowsBusinessRuleException()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.AddPhotoToAlbumAsync(Guid.NewGuid(), photo.Id, _caller));
    }

    // ─── GetAlbumPhotos ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetAlbumPhotos_ReturnsPhotosInAlbum()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);
        var photo1 = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "one.jpg");
        var photo2 = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "two.jpg");
        await _service.AddPhotoToAlbumAsync(album.Id, photo1.Id, _caller);
        await _service.AddPhotoToAlbumAsync(album.Id, photo2.Id, _caller);

        var result = await _service.GetAlbumPhotosAsync(album.Id, _caller);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetAlbumPhotos_EmptyAlbum_ReturnsEmpty()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);

        var result = await _service.GetAlbumPhotosAsync(album.Id, _caller);

        Assert.AreEqual(0, result.Count);
    }
}
