using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class SlideshowServiceTests
{
    private PhotosDbContext _db;
    private SlideshowService _service;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new SlideshowService(_db, NullLogger<SlideshowService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── CreateFromAlbum ──────────────────────────────────────────────

    [TestMethod]
    public async Task CreateFromAlbum_ValidAlbum_ReturnsSlideshowDto()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);
        var photo1 = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "slide1.jpg");
        var photo2 = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "slide2.jpg");
        _db.AlbumPhotos.Add(new AlbumPhoto { AlbumId = album.Id, PhotoId = photo1.Id, SortOrder = 0 });
        _db.AlbumPhotos.Add(new AlbumPhoto { AlbumId = album.Id, PhotoId = photo2.Id, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _service.CreateFromAlbumAsync(album.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.PhotoIds.Count);
        Assert.AreEqual(5, result.IntervalSeconds);
        Assert.AreEqual(SlideshowTransition.Fade, result.Transition);
    }

    [TestMethod]
    public async Task CreateFromAlbum_CustomInterval_UsesCustomValue()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);
        _db.AlbumPhotos.Add(new AlbumPhoto { AlbumId = album.Id, PhotoId = photo.Id, SortOrder = 0 });
        await _db.SaveChangesAsync();

        var result = await _service.CreateFromAlbumAsync(album.Id, intervalSeconds: 10, transition: SlideshowTransition.SlideHorizontal);

        Assert.AreEqual(10, result.IntervalSeconds);
        Assert.AreEqual(SlideshowTransition.SlideHorizontal, result.Transition);
    }

    [TestMethod]
    public async Task CreateFromAlbum_EmptyAlbum_ReturnsSlideshowWithNoPhotos()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _caller.UserId);

        var result = await _service.CreateFromAlbumAsync(album.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.PhotoIds.Count);
    }

    // ─── CreateFromSelection ──────────────────────────────────────────

    [TestMethod]
    public async Task CreateFromSelection_ValidPhotoIds_ReturnsSlideshowDto()
    {
        var photo1 = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "sel1.jpg");
        var photo2 = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId, "sel2.jpg");

        var result = await _service.CreateFromSelectionAsync(
            [photo1.Id, photo2.Id], intervalSeconds: 3, transition: SlideshowTransition.SlideVertical);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.PhotoIds.Count);
        Assert.AreEqual(3, result.IntervalSeconds);
        Assert.AreEqual(SlideshowTransition.SlideVertical, result.Transition);
    }

    [TestMethod]
    public async Task CreateFromSelection_EmptyList_ReturnsSlideshowWithNoPhotos()
    {
        var result = await _service.CreateFromSelectionAsync([]);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.PhotoIds.Count);
    }

    [TestMethod]
    public async Task CreateFromSelection_DefaultTransition_IsFade()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var result = await _service.CreateFromSelectionAsync([photo.Id]);

        Assert.AreEqual(SlideshowTransition.Fade, result.Transition);
    }
}
