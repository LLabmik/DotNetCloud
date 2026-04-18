using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class PhotoServiceTests
{
    private PhotosDbContext _db;
    private PhotoService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _service = new PhotoService(_db, _eventBusMock.Object, NullLogger<PhotoService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreatePhoto_ValidInput_ReturnsPhotoDto()
    {
        var fileNodeId = Guid.NewGuid();

        var result = await _service.CreatePhotoAsync(fileNodeId, "photo.jpg", "image/jpeg", 2048, _caller.UserId, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(fileNodeId, result.FileNodeId);
        Assert.AreEqual("photo.jpg", result.FileName);
        Assert.AreEqual("image/jpeg", result.MimeType);
        Assert.AreEqual(2048L, result.SizeBytes);
        Assert.AreEqual(_caller.UserId, result.OwnerId);
    }

    [TestMethod]
    public async Task CreatePhoto_PublishesPhotoUploadedEvent()
    {
        var fileNodeId = Guid.NewGuid();

        await _service.CreatePhotoAsync(fileNodeId, "event.jpg", "image/jpeg", 1024, _caller.UserId, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<PhotoUploadedEvent>(e => e.FileNodeId == fileNodeId && e.OwnerId == _caller.UserId),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreatePhoto_PersistedToDatabase()
    {
        var fileNodeId = Guid.NewGuid();

        var result = await _service.CreatePhotoAsync(fileNodeId, "db.jpg", "image/jpeg", 512, _caller.UserId, _caller);

        Assert.AreEqual(1, _db.Photos.Count());
        Assert.AreEqual(result.Id, _db.Photos.First().Id);
    }

    // ─── Get ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetPhoto_AsOwner_ReturnsPhoto()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var result = await _service.GetPhotoAsync(photo.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(photo.Id, result.Id);
    }

    [TestMethod]
    public async Task GetPhoto_NonOwnerNoShare_ReturnsNull()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, Guid.NewGuid());

        var result = await _service.GetPhotoAsync(photo.Id, _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetPhoto_NonOwnerWithShare_ReturnsPhoto()
    {
        var ownerId = Guid.NewGuid();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);
        await TestHelpers.SeedPhotoShareAsync(_db, photo.Id, ownerId, _caller.UserId);

        var result = await _service.GetPhotoAsync(photo.Id, _caller);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetPhoto_NonExistent_ReturnsNull()
    {
        var result = await _service.GetPhotoAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    // ─── List ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListPhotos_ReturnsOnlyOwnPhotos()
    {
        await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "mine.jpg");
        await TestHelpers.SeedPhotoAsync(_db, Guid.NewGuid(), "other.jpg");

        var result = await _service.ListPhotosAsync(_caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("mine.jpg", result[0].FileName);
    }

    [TestMethod]
    public async Task ListPhotos_Pagination_RespectsSkipAndTake()
    {
        for (int i = 0; i < 5; i++)
            await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, $"photo{i}.jpg");

        var result = await _service.ListPhotosAsync(_caller, skip: 2, take: 2);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task ListPhotos_EmptyForNewUser_ReturnsEmpty()
    {
        var result = await _service.ListPhotosAsync(_caller);
        Assert.AreEqual(0, result.Count);
    }

    // ─── Timeline ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTimeline_ReturnsPhotosInDateRange()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow.AddDays(1);
        var result = await _service.GetTimelineAsync(_caller, from, to);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task GetTimeline_ExcludesPhotosOutsideRange()
    {
        await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow.AddDays(-20);
        var result = await _service.GetTimelineAsync(_caller, from, to);

        Assert.AreEqual(0, result.Count);
    }

    // ─── Favorites ────────────────────────────────────────────────────

    [TestMethod]
    public async Task ToggleFavorite_SetsToTrue()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        Assert.IsFalse(photo.IsFavorite);

        var result = await _service.ToggleFavoriteAsync(photo.Id, _caller);

        Assert.IsTrue(result.IsFavorite);
    }

    [TestMethod]
    public async Task ToggleFavorite_Twice_SetsBackToFalse()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        await _service.ToggleFavoriteAsync(photo.Id, _caller);
        var result = await _service.ToggleFavoriteAsync(photo.Id, _caller);

        Assert.IsFalse(result.IsFavorite);
    }

    [TestMethod]
    public async Task ToggleFavorite_NonExistent_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ToggleFavoriteAsync(Guid.NewGuid(), _caller));
    }

    [TestMethod]
    public async Task GetFavorites_ReturnsOnlyFavorites()
    {
        var photo1 = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "fav.jpg");
        await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "nonfav.jpg");
        await _service.ToggleFavoriteAsync(photo1.Id, _caller);

        var result = await _service.GetFavoritesAsync(_caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("fav.jpg", result[0].FileName);
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeletePhoto_SoftDeletes()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        await _service.DeletePhotoAsync(photo.Id, _caller);

        // Query filter should exclude it
        Assert.AreEqual(0, _db.Photos.Count());
        // But it still exists in raw data
        Assert.AreEqual(1, _db.Photos.IgnoreQueryFilters().Count());
    }

    [TestMethod]
    public async Task DeletePhoto_NonExistent_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.DeletePhotoAsync(Guid.NewGuid(), _caller));
    }

    // ─── Search ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task Search_ByFileName_ReturnsMatches()
    {
        await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "sunset_beach.jpg");
        await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "mountain_view.jpg");

        var result = await _service.SearchAsync(_caller, "sunset");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("sunset_beach.jpg", result[0].FileName);
    }

    [TestMethod]
    public async Task Search_EmptyQuery_ReturnsEmpty()
    {
        await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var result = await _service.SearchAsync(_caller, "nonexistent_query_xyz");

        Assert.AreEqual(0, result.Count);
    }
}
