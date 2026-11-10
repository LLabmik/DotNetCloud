using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// The non-destructive edit stack: applying it bakes the operations into the cached previews while
/// the original (read through the core) is never modified.
/// </summary>
[TestClass]
public sealed class PhotoThumbnailServiceSaveEditsTests
{
    private PhotosDbContext _db = null!;

    [TestInitialize]
    public void Setup() => _db = TestHelpers.CreateDb();

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private static byte[] CreateTestImage(int width = 400, int height = 300)
    {
        using var image = new Image<Rgba32>(Configuration.Default, width, height);
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private PhotoThumbnailService CreateService(FakeFileModuleApi? coreApi) =>
        new(_db, NullLogger<PhotoThumbnailService>.Instance, configuration: null, coreApi: coreApi);

    private async Task<Photo> SeedPhotoWithEditsAsync(
        Guid ownerId,
        string mimeType = "image/jpeg",
        params (string EditType, string ParametersJson)[] edits)
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId, mimeType: mimeType);

        for (var index = 0; index < edits.Length; index++)
        {
            _db.PhotoEditRecords.Add(new PhotoEditRecord
            {
                PhotoId = photo.Id,
                OperationType = edits[index].EditType,
                ParametersJson = edits[index].ParametersJson,
                StackOrder = index,
                EditedByUserId = ownerId,
            });
        }

        await _db.SaveChangesAsync();
        return photo;
    }

    [TestMethod]
    public async Task SaveEditsAsync_PhotoNotFound_ReturnsFalse()
    {
        var service = CreateService(new FakeFileModuleApi(CreateTestImage()));

        var result = await service.SaveEditsAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SaveEditsAsync_CallerIsNotTheOwner_ReturnsFalse()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await SeedPhotoWithEditsAsync(ownerId, "image/jpeg", ("Rotate", """{"value":"90"}"""));

        var coreApi = new FakeFileModuleApi(CreateTestImage());
        var service = CreateService(coreApi);

        var result = await service.SaveEditsAsync(photo.Id, Guid.CreateVersion7());

        Assert.IsFalse(result);
        Assert.AreEqual(0, coreApi.DownloadedNodeIds.Count);
    }

    [TestMethod]
    public async Task SaveEditsAsync_UnsupportedMimeType_ReturnsFalse()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await SeedPhotoWithEditsAsync(ownerId, "application/pdf", ("Rotate", """{"value":"90"}"""));

        var service = CreateService(new FakeFileModuleApi(CreateTestImage()));

        Assert.IsFalse(await service.SaveEditsAsync(photo.Id, ownerId));
    }

    [TestMethod]
    public async Task SaveEditsAsync_NoEditRecords_ReturnsTrueWithoutReadingTheOriginal()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await TestHelpers.SeedPhotoAsync(_db, ownerId);

        var coreApi = new FakeFileModuleApi(CreateTestImage());
        var service = CreateService(coreApi);

        Assert.IsTrue(await service.SaveEditsAsync(photo.Id, ownerId));
        Assert.AreEqual(0, coreApi.DownloadedNodeIds.Count);
    }

    [TestMethod]
    public async Task SaveEditsAsync_ConsentMissing_ThrowsForbiddenSoTheUiCanAskForIt()
    {
        // Saving needs the original, i.e. the core's `files:read` consent. Swallowing the refusal made
        // the module answer a bare 500 ("Failed to apply edits to thumbnails") and the user had no way to
        // learn that a consent prompt was all that was needed.
        var ownerId = Guid.CreateVersion7();
        var photo = await SeedPhotoWithEditsAsync(ownerId, "image/jpeg", ("Rotate", """{"value":"90"}"""));

        var trailers = new Metadata { { "error-code", "MODULE_CONSENT_REQUIRED" } };
        var refusal = new RpcException(
            new Status(
                StatusCode.PermissionDenied,
                "[MODULE_CONSENT_REQUIRED] The user has not consented to the scopes this call needs: files:read."),
            trailers);

        var service = CreateService(new FakeFileModuleApi(refusal));

        var ex = await Assert.ThrowsExactlyAsync<FilesConsentRequiredException>(
            () => service.SaveEditsAsync(photo.Id, ownerId));

        Assert.AreEqual(FilesConsentRequiredException.Code, ex.ErrorCode);
    }

    [TestMethod]
    public async Task SaveEditsAsync_OriginalUnavailable_ReturnsFalse()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await SeedPhotoWithEditsAsync(ownerId, "image/jpeg", ("Rotate", """{"value":"90"}"""));

        var service = CreateService(new FakeFileModuleApi(content: null));

        Assert.IsFalse(await service.SaveEditsAsync(photo.Id, ownerId));
    }

    [TestMethod]
    public async Task SaveEditsAsync_RotateEdit_RegeneratesBothThumbnails()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await SeedPhotoWithEditsAsync(ownerId, "image/jpeg", ("Rotate", """{"value":"90"}"""));

        var service = CreateService(new FakeFileModuleApi(CreateTestImage(400, 300)));

        Assert.IsTrue(await service.SaveEditsAsync(photo.Id, ownerId));

        var stored = _db.Photos.Single(p => p.Id == photo.Id);
        Assert.IsNotNull(stored.ThumbnailGrid);
        Assert.IsNotNull(stored.ThumbnailDetail);

        // A quarter turn swaps the aspect ratio, proving the stack was applied before resizing.
        using var detail = Image.Load(stored.ThumbnailDetail!);
        Assert.IsTrue(detail.Height > detail.Width);
    }

    [TestMethod]
    [DataRow("Flip", """{"value":"0"}""")]
    [DataRow("Flip", """{"value":"1"}""")]
    [DataRow("Brightness", """{"value":"20"}""")]
    [DataRow("Brightness", """{"value":"-20"}""")]
    [DataRow("Contrast", """{"value":"15"}""")]
    [DataRow("Saturation", """{"value":"30"}""")]
    [DataRow("Blur", """{"value":"2"}""")]
    [DataRow("Sharpen", """{"value":"1"}""")]
    [DataRow("Blur", """{"value":"0"}""")]
    [DataRow("Sharpen", """{"value":"0"}""")]
    public async Task SaveEditsAsync_EverySupportedOperation_RegeneratesThumbnails(string editType, string parametersJson)
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await SeedPhotoWithEditsAsync(ownerId, "image/jpeg", (editType, parametersJson));

        var service = CreateService(new FakeFileModuleApi(CreateTestImage()));

        Assert.IsTrue(await service.SaveEditsAsync(photo.Id, ownerId));

        var stored = _db.Photos.Single(p => p.Id == photo.Id);
        Assert.IsTrue(stored.ThumbnailDetail is { Length: > 0 });
        Assert.IsTrue(stored.ThumbnailGrid is { Length: > 0 });
    }

    [TestMethod]
    [DataRow("Unknown", """{"value":"1"}""")]
    [DataRow("Rotate", """{"other":"90"}""")]
    [DataRow("Rotate", """{"value":"not-a-number"}""")]
    [DataRow("Rotate", "{}")]
    public async Task SaveEditsAsync_UnusableEditRecord_IsSkippedButStillSucceeds(string editType, string parametersJson)
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await SeedPhotoWithEditsAsync(ownerId, "image/jpeg", (editType, parametersJson));

        var service = CreateService(new FakeFileModuleApi(CreateTestImage()));

        Assert.IsTrue(await service.SaveEditsAsync(photo.Id, ownerId));
    }

    [TestMethod]
    public async Task SaveEditsAsync_MultipleEdits_AreAppliedInStackOrder()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await SeedPhotoWithEditsAsync(
            ownerId,
            "image/jpeg",
            ("Rotate", """{"value":"90"}"""),
            ("Rotate", """{"value":"90"}"""));

        var service = CreateService(new FakeFileModuleApi(CreateTestImage(400, 300)));

        Assert.IsTrue(await service.SaveEditsAsync(photo.Id, ownerId));

        using var detail = Image.Load(_db.Photos.Single(p => p.Id == photo.Id).ThumbnailDetail!);

        // Two quarter turns restore the original landscape orientation.
        Assert.IsTrue(detail.Width > detail.Height);
    }

    [TestMethod]
    public async Task SaveEditsAsync_Success_UpdatesTimestampAndProducesValidJpeg()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await SeedPhotoWithEditsAsync(ownerId, "image/jpeg", ("Rotate", """{"value":"90"}"""));
        var before = photo.UpdatedAt;

        await Task.Delay(10);
        var service = CreateService(new FakeFileModuleApi(CreateTestImage()));

        Assert.IsTrue(await service.SaveEditsAsync(photo.Id, ownerId));

        var stored = _db.Photos.Single(p => p.Id == photo.Id);
        Assert.IsTrue(stored.UpdatedAt >= before);

        using var image = await Image.LoadAsync(new MemoryStream(stored.ThumbnailGrid!));
        Assert.AreEqual("jpeg", image.Metadata.DecodedImageFormat?.Name.ToLowerInvariant());
    }

    [TestMethod]
    public async Task SaveEditsAsync_ReadsThePhotoFileNodeThroughTheCore()
    {
        var ownerId = Guid.CreateVersion7();
        var photo = await SeedPhotoWithEditsAsync(ownerId, "image/jpeg", ("Rotate", """{"value":"90"}"""));

        var coreApi = new FakeFileModuleApi(CreateTestImage());
        var service = CreateService(coreApi);

        Assert.IsTrue(await service.SaveEditsAsync(photo.Id, ownerId));
        CollectionAssert.AreEqual(new[] { photo.FileNodeId.ToString() }, coreApi.DownloadedNodeIds);
    }
}
