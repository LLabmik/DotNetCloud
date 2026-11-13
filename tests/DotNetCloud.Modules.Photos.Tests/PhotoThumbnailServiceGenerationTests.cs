using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Thumbnail generation in the sandboxed model: the original is read through the core module API on
/// the acting user's behalf, so only the owner can generate and a host without the core API fails
/// closed instead of touching a filesystem.
/// </summary>
[TestClass]
public sealed class PhotoThumbnailServiceGenerationTests
{
    private PhotosDbContext _db = null!;

    [TestInitialize]
    public void Setup() => _db = TestHelpers.CreateDb();

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private static byte[] CreateTestImage(int width = 800, int height = 600)
    {
        using var image = new Image<Rgba32>(Configuration.Default, width, height);
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private PhotoThumbnailService CreateService(FakeFileModuleApi? coreApi) =>
        new(_db, NullLogger<PhotoThumbnailService>.Instance, configuration: null, coreApi: coreApi);

    [TestMethod]
    public async Task GenerateThumbnailsAsync_WhenBothThumbnailsExist_SkipsWithoutReadingTheOriginal()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);
        photo.ThumbnailGrid = [1, 2, 3];
        photo.ThumbnailDetail = [4, 5, 6];
        await _db.SaveChangesAsync();

        var coreApi = new FakeFileModuleApi(CreateTestImage());
        var service = CreateService(coreApi);

        var result = await service.GenerateThumbnailsAsync(photo.Id, ownerId);

        Assert.IsTrue(result);
        Assert.AreEqual(0, coreApi.DownloadedNodeIds.Count);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, _db.Photos.Single(p => p.Id == photo.Id).ThumbnailGrid);
    }

    [TestMethod]
    public async Task GenerateThumbnailsAsync_UnsupportedMimeType_DoesNotReadTheOriginal()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId, mimeType: "image/svg+xml");

        var coreApi = new FakeFileModuleApi(CreateTestImage());
        var service = CreateService(coreApi);

        var result = await service.GenerateThumbnailsAsync(photo.Id, ownerId);

        Assert.IsFalse(result);
        Assert.AreEqual(0, coreApi.DownloadedNodeIds.Count);
        Assert.IsNull(_db.Photos.Single(p => p.Id == photo.Id).ThumbnailDetail);
    }

    [TestMethod]
    public async Task GenerateThumbnailsAsync_NewPhoto_StoresGridAndDetailInOnePass()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);

        var coreApi = new FakeFileModuleApi(CreateTestImage());
        var service = CreateService(coreApi);

        var result = await service.GenerateThumbnailsAsync(photo.Id, ownerId);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(
            new[] { photo.FileNodeId.ToString() },
            coreApi.DownloadedNodeIds);

        var stored = _db.Photos.Single(p => p.Id == photo.Id);
        Assert.IsNotNull(stored.ThumbnailGrid);
        Assert.IsNotNull(stored.ThumbnailDetail);
        Assert.IsTrue(stored.ThumbnailDetail!.Length > 0);
        Assert.IsTrue(stored.ThumbnailGrid!.Length > 0);
    }

    [TestMethod]
    public async Task GenerateThumbnailsAsync_CallerIsNotTheOwner_IsRefused()
    {
        var ownerId = Guid.CreateVersion7();
        var intruderId = Guid.CreateVersion7();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);

        var coreApi = new FakeFileModuleApi(CreateTestImage());
        var service = CreateService(coreApi);

        var result = await service.GenerateThumbnailsAsync(photo.Id, intruderId);

        Assert.IsFalse(result);
        Assert.AreEqual(0, coreApi.DownloadedNodeIds.Count);
        Assert.IsNull(_db.Photos.Single(p => p.Id == photo.Id).ThumbnailDetail);
    }

    [TestMethod]
    public async Task GenerateThumbnailsAsync_OriginalUnavailable_ReturnsFalseAndStoresNothing()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);

        var service = CreateService(new FakeFileModuleApi(content: null));

        var result = await service.GenerateThumbnailsAsync(photo.Id, ownerId);

        Assert.IsFalse(result);
        Assert.IsNull(_db.Photos.Single(p => p.Id == photo.Id).ThumbnailDetail);
    }

    [TestMethod]
    public async Task GenerateThumbnailsAsync_NoCoreApi_ReturnsFalse()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);

        var service = CreateService(coreApi: null);

        var result = await service.GenerateThumbnailsAsync(photo.Id, ownerId);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetThumbnailAsync_WhenCacheIsEmpty_GeneratesThenServes()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);

        var service = CreateService(new FakeFileModuleApi(CreateTestImage()));

        var (data, contentType) = await service.GetThumbnailAsync(
            photo.Id, ownerId, DotNetCloud.Modules.Photos.Services.PhotoThumbnailSize.Grid);

        Assert.IsNotNull(data);
        Assert.AreEqual("image/jpeg", contentType);

        using var image = await Image.LoadAsync(data!);
        Assert.IsTrue(image.Width <= 300);
    }

    [TestMethod]
    public async Task GetThumbnailAsync_FullSize_ReturnsNull()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);

        var service = CreateService(new FakeFileModuleApi(CreateTestImage()));

        var (data, contentType) = await service.GetThumbnailAsync(
            photo.Id, ownerId, DotNetCloud.Modules.Photos.Services.PhotoThumbnailSize.Full);

        Assert.IsNull(data);
        Assert.IsNull(contentType);
    }

    [TestMethod]
    public async Task DeleteThumbnailsAsync_ClearsBothSizes()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);
        photo.ThumbnailGrid = [1];
        photo.ThumbnailDetail = [2];
        await _db.SaveChangesAsync();

        var service = CreateService(coreApi: null);
        await service.DeleteThumbnailsAsync(photo.Id);

        var stored = _db.Photos.Single(p => p.Id == photo.Id);
        Assert.IsNull(stored.ThumbnailGrid);
        Assert.IsNull(stored.ThumbnailDetail);
    }
}
