using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class PhotoMetadataServiceTests
{
    private PhotosDbContext _db;
    private PhotoMetadataService _service;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new PhotoMetadataService(_db, NullLogger<PhotoMetadataService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── GetMetadata ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetMetadata_ExistingMetadata_ReturnsDto()
    {
        var photo = await TestHelpers.SeedPhotoWithMetadataAsync(_db, _caller.UserId, latitude: 48.85, longitude: 2.35);

        var result = await _service.GetMetadataAsync(photo.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual("Canon", result.CameraMake);
        Assert.AreEqual("EOS R5", result.CameraModel);
        Assert.AreEqual(200, result.Iso);
    }

    [TestMethod]
    public async Task GetMetadata_GeoData_IncludesCoordinates()
    {
        var photo = await TestHelpers.SeedPhotoWithMetadataAsync(_db, _caller.UserId, latitude: 48.8566, longitude: 2.3522);

        var result = await _service.GetMetadataAsync(photo.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(48.8566, result.Latitude!.Value, 0.001);
        Assert.AreEqual(2.3522, result.Longitude!.Value, 0.001);
    }

    [TestMethod]
    public async Task GetMetadata_NoMetadata_ReturnsNull()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        var result = await _service.GetMetadataAsync(photo.Id);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMetadata_NonExistentPhoto_ReturnsNull()
    {
        var result = await _service.GetMetadataAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    // ─── ExtractAndStore ──────────────────────────────────────────────

    [TestMethod]
    public async Task ExtractAndStore_WithNoExtractor_StoresBasicMetadata()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _caller.UserId);

        // Service created without exifExtractor, so extraction is skipped/basic
        await _service.ExtractAndStoreAsync(photo.Id, "/tmp/test.jpg", "image/jpeg");

        // Should create metadata record even without EXIF extractor
        var metadata = _db.PhotoMetadata.FirstOrDefault(m => m.PhotoId == photo.Id);
        Assert.IsNotNull(metadata);
    }
}
