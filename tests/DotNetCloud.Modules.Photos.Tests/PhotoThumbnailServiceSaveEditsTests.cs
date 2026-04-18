using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class PhotoThumbnailServiceSaveEditsTests
{
    private PhotosDbContext _db = null!;
    private Mock<IFileStorageEngine> _storageEngineMock = null!;
    private Mock<IDownloadService> _downloadServiceMock = null!;
    private PhotoThumbnailService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _storageEngineMock = new Mock<IFileStorageEngine>();
        _downloadServiceMock = new Mock<IDownloadService>();
        _service = new PhotoThumbnailService(
            _db,
            _storageEngineMock.Object,
            _downloadServiceMock.Object,
            NullLogger<PhotoThumbnailService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Helpers ─────────────────────────────────────────────────────

    private static Stream CreateTestImageStream(int width = 100, int height = 100)
    {
        var ms = new MemoryStream();
        using (var image = new Image<Rgba32>(width, height, Color.CornflowerBlue))
        {
            image.SaveAsJpeg(ms);
        }
        ms.Position = 0;
        return ms;
    }

    private async Task<Photo> SeedPhotoWithEditsAsync(
        Guid ownerId,
        string mimeType = "image/jpeg",
        params (string editType, string paramJson)[] edits)
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId, mimeType: mimeType);

        for (int i = 0; i < edits.Length; i++)
        {
            _db.PhotoEditRecords.Add(new PhotoEditRecord
            {
                PhotoId = photo.Id,
                OperationType = edits[i].editType,
                ParametersJson = edits[i].paramJson,
                StackOrder = i,
                EditedByUserId = ownerId
            });
        }
        await _db.SaveChangesAsync();
        return photo;
    }

    private void SetupDownloadWithImage(Guid fileNodeId, int width = 100, int height = 100)
    {
        _downloadServiceMock
            .Setup(d => d.DownloadCurrentAsync(fileNodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestImageStream(width, height));
    }

    // ─── Photo Not Found ─────────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_PhotoNotFound_ReturnsFalse()
    {
        var result = await _service.SaveEditsAsync(Guid.NewGuid());

        Assert.IsFalse(result);
    }

    // ─── Unsupported MIME Type ───────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_UnsupportedMimeType_ReturnsFalse()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await TestHelpers.SeedPhotoAsync(_db, caller.UserId, mimeType: "application/pdf");

        _db.PhotoEditRecords.Add(new PhotoEditRecord
        {
            PhotoId = photo.Id,
            OperationType = "Rotate",
            ParametersJson = """{"value":"90"}""",
            StackOrder = 0,
            EditedByUserId = caller.UserId
        });
        await _db.SaveChangesAsync();

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsFalse(result);
    }

    // ─── No Edits ────────────────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_NoEditRecords_ReturnsTrue()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await TestHelpers.SeedPhotoAsync(_db, caller.UserId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
        // Should not have called download service at all
        _downloadServiceMock.Verify(
            d => d.DownloadCurrentAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Download Service Throws ─────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_DownloadServiceThrows_ReturnsFalse()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{"value":"90"}"""));

        _downloadServiceMock
            .Setup(d => d.DownloadCurrentAsync(photo.FileNodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk read failure"));

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsFalse(result);
    }

    // ─── Successful Save: Rotate ─────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_RotateEdit_RegeneratesThumbnails()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{"value":"90"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);

        var updated = await _db.Photos.FindAsync(photo.Id);
        Assert.IsNotNull(updated!.ThumbnailGrid);
        Assert.IsNotNull(updated.ThumbnailDetail);
        Assert.IsTrue(updated.ThumbnailGrid!.Length > 0);
        Assert.IsTrue(updated.ThumbnailDetail!.Length > 0);
    }

    // ─── Successful Save: Flip Horizontal ────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_FlipHorizontal_RegeneratesThumbnails()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Flip", """{"value":"0"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
        var updated = await _db.Photos.FindAsync(photo.Id);
        Assert.IsNotNull(updated!.ThumbnailGrid);
        Assert.IsNotNull(updated.ThumbnailDetail);
    }

    // ─── Successful Save: Flip Vertical ──────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_FlipVertical_RegeneratesThumbnails()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Flip", """{"value":"1"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Successful Save: Brightness ─────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_BrightnessEdit_RegeneratesThumbnails()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Brightness", """{"value":"25"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
        var updated = await _db.Photos.FindAsync(photo.Id);
        Assert.IsNotNull(updated!.ThumbnailGrid);
    }

    // ─── Successful Save: Contrast ───────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_ContrastEdit_RegeneratesThumbnails()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Contrast", """{"value":"30"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Successful Save: Saturation ─────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_SaturationEdit_RegeneratesThumbnails()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Saturation", """{"value":"-20"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Successful Save: Blur ───────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_BlurEdit_RegeneratesThumbnails()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Blur", """{"value":"5"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Successful Save: Sharpen ────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_SharpenEdit_RegeneratesThumbnails()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Sharpen", """{"value":"3"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Blur With Zero Value ────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_BlurZeroValue_StillSucceeds()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Blur", """{"value":"0"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Sharpen With Zero Value ─────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_SharpenZeroValue_StillSucceeds()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Sharpen", """{"value":"0"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Multiple Edits ──────────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_MultipleEdits_AppliesAllInOrder()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{"value":"90"}"""),
            ("Flip", """{"value":"0"}"""),
            ("Brightness", """{"value":"10"}"""),
            ("Contrast", """{"value":"20"}"""),
            ("Saturation", """{"value":"-15"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
        var updated = await _db.Photos.FindAsync(photo.Id);
        Assert.IsNotNull(updated!.ThumbnailGrid);
        Assert.IsNotNull(updated.ThumbnailDetail);
        Assert.IsTrue(updated.ThumbnailGrid!.Length > 0);
        Assert.IsTrue(updated.ThumbnailDetail!.Length > 0);
    }

    // ─── UpdatedAt Timestamp ─────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_Success_UpdatesTimestamp()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{"value":"180"}"""));

        var originalUpdatedAt = photo.UpdatedAt;

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
        var updated = await _db.Photos.FindAsync(photo.Id);
        Assert.IsTrue(updated!.UpdatedAt >= originalUpdatedAt);
    }

    // ─── Unknown Edit Type ───────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_UnknownEditType_SkippedGracefully()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("FutureEditType", """{"value":"42"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Missing Value Parameter ─────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_MissingValueParameter_SkippedGracefully()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{"degrees":"90"}""")); // Wrong key

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Non-Numeric Value Parameter ─────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_NonNumericValue_SkippedGracefully()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{"value":"abc"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Supported MIME Types ────────────────────────────────────────

    [TestMethod]
    [DataRow("image/jpeg")]
    [DataRow("image/jpg")]
    [DataRow("image/png")]
    [DataRow("image/gif")]
    [DataRow("image/webp")]
    [DataRow("image/bmp")]
    [DataRow("image/tiff")]
    public async Task SaveEditsAsync_SupportedMimeType_Succeeds(string mimeType)
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            mimeType,
            ("Rotate", """{"value":"90"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Unsupported MIME Types ──────────────────────────────────────

    [TestMethod]
    [DataRow("application/pdf")]
    [DataRow("video/mp4")]
    [DataRow("audio/mpeg")]
    [DataRow("text/plain")]
    public async Task SaveEditsAsync_UnsupportedMimeType_ReturnsFalse(string mimeType)
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            mimeType,
            ("Rotate", """{"value":"90"}"""));

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsFalse(result);
    }

    // ─── Detail Thumbnail Is Larger Than Grid ────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_DetailThumbnailLargerThanGrid()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Brightness", """{"value":"10"}"""));

        SetupDownloadWithImage(photo.FileNodeId, 2000, 2000);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
        var updated = await _db.Photos.FindAsync(photo.Id);
        Assert.IsTrue(updated!.ThumbnailDetail!.Length > updated.ThumbnailGrid!.Length,
            "Detail thumbnail should be larger than grid thumbnail for a 2000px source image");
    }

    // ─── Thumbnails Are Valid JPEG ───────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_GeneratedThumbnails_AreValidJpeg()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{"value":"90"}"""));

        SetupDownloadWithImage(photo.FileNodeId, 500, 500);

        await _service.SaveEditsAsync(photo.Id);

        var updated = await _db.Photos.FindAsync(photo.Id);

        // Verify JPEG magic bytes (FFD8FF)
        Assert.AreEqual(0xFF, updated!.ThumbnailGrid![0]);
        Assert.AreEqual(0xD8, updated.ThumbnailGrid[1]);
        Assert.AreEqual(0xFF, updated.ThumbnailGrid[2]);

        Assert.AreEqual(0xFF, updated.ThumbnailDetail![0]);
        Assert.AreEqual(0xD8, updated.ThumbnailDetail[1]);
        Assert.AreEqual(0xFF, updated.ThumbnailDetail[2]);
    }

    // ─── Download Service Called With Correct FileNodeId ─────────────

    [TestMethod]
    public async Task SaveEditsAsync_CallsDownloadServiceWithCorrectFileNodeId()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{"value":"90"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        await _service.SaveEditsAsync(photo.Id);

        _downloadServiceMock.Verify(
            d => d.DownloadCurrentAsync(photo.FileNodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Download Service Called With System Caller ───────────────────

    [TestMethod]
    public async Task SaveEditsAsync_UsesSystemCallerContext()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{"value":"90"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        await _service.SaveEditsAsync(photo.Id);

        _downloadServiceMock.Verify(
            d => d.DownloadCurrentAsync(
                photo.FileNodeId,
                It.Is<CallerContext>(c => c.Type == CallerType.System && c.UserId == photo.OwnerId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Overwrites Previous Thumbnails ──────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_OverwritesPreviousThumbnails()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{"value":"90"}"""));

        // Seed existing thumbnail data
        photo.ThumbnailGrid = new byte[] { 1, 2, 3 };
        photo.ThumbnailDetail = new byte[] { 4, 5, 6 };
        await _db.SaveChangesAsync();

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
        var updated = await _db.Photos.FindAsync(photo.Id);
        Assert.AreNotEqual(3, updated!.ThumbnailGrid!.Length);
        Assert.AreNotEqual(3, updated.ThumbnailDetail!.Length);
    }

    // ─── Negative Brightness ─────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_NegativeBrightness_Succeeds()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Brightness", """{"value":"-50"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Negative Contrast ───────────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_NegativeContrast_Succeeds()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Contrast", """{"value":"-30"}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Empty Parameters JSON ───────────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_EmptyParametersJson_SkippedGracefully()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await SeedPhotoWithEditsAsync(
            caller.UserId,
            "image/jpeg",
            ("Rotate", """{}"""));

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }

    // ─── Edits Applied In StackOrder ─────────────────────────────────

    [TestMethod]
    public async Task SaveEditsAsync_EditsAppliedInStackOrder()
    {
        var caller = TestHelpers.CreateCaller();
        var photo = await TestHelpers.SeedPhotoAsync(_db, caller.UserId);

        // Add edits with non-sequential insertion but sequential StackOrder
        _db.PhotoEditRecords.Add(new PhotoEditRecord
        {
            PhotoId = photo.Id,
            OperationType = "Rotate",
            ParametersJson = """{"value":"90"}""",
            StackOrder = 2,
            EditedByUserId = caller.UserId
        });
        _db.PhotoEditRecords.Add(new PhotoEditRecord
        {
            PhotoId = photo.Id,
            OperationType = "Brightness",
            ParametersJson = """{"value":"10"}""",
            StackOrder = 0,
            EditedByUserId = caller.UserId
        });
        _db.PhotoEditRecords.Add(new PhotoEditRecord
        {
            PhotoId = photo.Id,
            OperationType = "Flip",
            ParametersJson = """{"value":"0"}""",
            StackOrder = 1,
            EditedByUserId = caller.UserId
        });
        await _db.SaveChangesAsync();

        SetupDownloadWithImage(photo.FileNodeId);

        var result = await _service.SaveEditsAsync(photo.Id);

        Assert.IsTrue(result);
    }
}
