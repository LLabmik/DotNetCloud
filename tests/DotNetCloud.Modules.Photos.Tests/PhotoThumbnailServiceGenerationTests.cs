using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public sealed class PhotoThumbnailServiceGenerationTests
{
    private PhotosDbContext _db = null!;
    private Mock<IFileStorageEngine> _storageEngineMock = null!;
    private Mock<IDownloadService> _downloadServiceMock = null!;
    private Mock<IThumbnailService> _filesThumbnailMock = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;
    private PhotoThumbnailService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _storageEngineMock = new Mock<IFileStorageEngine>();
        _downloadServiceMock = new Mock<IDownloadService>();
        _filesThumbnailMock = new Mock<IThumbnailService>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IFileStorageEngine))).Returns(_storageEngineMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDownloadService))).Returns(_downloadServiceMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IThumbnailService))).Returns(_filesThumbnailMock.Object);

        _service = new PhotoThumbnailService(
            _db, _serviceProviderMock.Object, NullLogger<PhotoThumbnailService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task GenerateThumbnailsAsync_WhenBothThumbnailsExist_SkipsWithoutReadingStorage()
    {
        var photo = await SeedPhotoAsync(grid: [1, 2, 3], detail: [4, 5, 6]);

        await _service.GenerateThumbnailsAsync(photo.Id, "files/aa/bb", photo.MimeType);

        _storageEngineMock.Verify(
            engine => engine.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _downloadServiceMock.Verify(
            download => download.DownloadCurrentAsync(It.IsAny<Guid>(), It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var stored = _db.Photos.Single(p => p.Id == photo.Id);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, stored.ThumbnailGrid);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, stored.ThumbnailDetail);
    }

    [TestMethod]
    public async Task GenerateThumbnailsAsync_UnsupportedMimeType_DoesNotReadStorage()
    {
        var photo = await SeedPhotoAsync(mimeType: "image/svg+xml");

        await _service.GenerateThumbnailsAsync(photo.Id, "files/aa/bb", "image/svg+xml");

        _storageEngineMock.Verify(
            engine => engine.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsNull(_db.Photos.Single(p => p.Id == photo.Id).ThumbnailDetail);
    }

    [TestMethod]
    public async Task GenerateThumbnailsAsync_NewPhoto_StoresGridAndDetailInOnePass()
    {
        var photo = await SeedPhotoAsync();
        _storageEngineMock
            .Setup(engine => engine.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestImageStream(800, 600));
        _filesThumbnailMock
            .Setup(files => files.GetThumbnailAsync(It.IsAny<Guid>(), It.IsAny<ThumbnailSize>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((Stream?)null, (string?)null));

        await _service.GenerateThumbnailsAsync(photo.Id, "files/aa/bb", photo.MimeType);

        var stored = _db.Photos.Single(p => p.Id == photo.Id);
        Assert.IsNotNull(stored.ThumbnailDetail);
        Assert.IsNotNull(stored.ThumbnailGrid);
        Assert.IsTrue(stored.ThumbnailDetail!.Length > 0);
        Assert.IsTrue(stored.ThumbnailGrid!.Length > 0);
        // The grid is a 300px derivative of the detail, so it must be the smaller payload.
        Assert.IsTrue(stored.ThumbnailGrid.Length < stored.ThumbnailDetail.Length);

        // Only one decode of the original: the storage stream is opened exactly once.
        _storageEngineMock.Verify(
            engine => engine.OpenReadStreamAsync("files/aa/bb", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GenerateThumbnailsAsync_FilesCachedThumbnail_IsUsedForGridWhenEnabled()
    {
        var photo = await SeedPhotoAsync();
        _storageEngineMock
            .Setup(engine => engine.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestImageStream(800, 600));
        _filesThumbnailMock
            .Setup(files => files.GetThumbnailAsync(photo.FileNodeId, ThumbnailSize.Large, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateTestImageStream(512, 384), "image/jpeg"));

        var service = new PhotoThumbnailService(
            _db,
            _serviceProviderMock.Object,
            NullLogger<PhotoThumbnailService>.Instance,
            CreateConfiguration(("Photos:Thumbnails:UseFilesCachedGridThumbnail", "true")));

        await service.GenerateThumbnailsAsync(photo.Id, "files/aa/bb", photo.MimeType);

        _filesThumbnailMock.Verify(
            files => files.GetThumbnailAsync(photo.FileNodeId, ThumbnailSize.Large, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.IsNotNull(_db.Photos.Single(p => p.Id == photo.Id).ThumbnailGrid);
    }

    [TestMethod]
    public async Task GenerateThumbnailsAsync_FilesCacheDisabledByDefault_IsNotQueried()
    {
        var photo = await SeedPhotoAsync();
        _storageEngineMock
            .Setup(engine => engine.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestImageStream(400, 300));

        await _service.GenerateThumbnailsAsync(photo.Id, "files/aa/bb", photo.MimeType);

        _filesThumbnailMock.Verify(
            files => files.GetThumbnailAsync(It.IsAny<Guid>(), It.IsAny<ThumbnailSize>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.IsNotNull(_db.Photos.Single(p => p.Id == photo.Id).ThumbnailGrid);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private async Task<Photo> SeedPhotoAsync(
        string mimeType = "image/jpeg",
        byte[]? grid = null,
        byte[]? detail = null)
    {
        var photo = new Photo
        {
            FileNodeId = Guid.CreateVersion7(),
            OwnerId = Guid.CreateVersion7(),
            FileName = "photo.jpg",
            MimeType = mimeType,
            SizeBytes = 1024,
            ThumbnailGrid = grid,
            ThumbnailDetail = detail,
        };

        _db.Photos.Add(photo);
        await _db.SaveChangesAsync();
        return photo;
    }

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values)
    {
        var map = values.ToDictionary(value => value.Key, value => (string?)value.Value);
        var mock = new Mock<IConfiguration>();
        mock.Setup(configuration => configuration[It.IsAny<string>()])
            .Returns((string key) => map.TryGetValue(key, out var value) ? value : null);
        return mock.Object;
    }

    private static Stream CreateTestImageStream(int width, int height)
    {
        var ms = new MemoryStream();
        using (var image = new Image<Rgba32>(Configuration.Default, width, height))
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    image[x, y] = new Rgba32((byte)(x % 256), (byte)(y % 256), (byte)((x + y) % 256));
                }
            }

            image.SaveAsJpeg(ms);
        }

        ms.Position = 0;
        return ms;
    }
}
