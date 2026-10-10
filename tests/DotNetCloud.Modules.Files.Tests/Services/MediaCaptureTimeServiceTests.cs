using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="MediaCaptureTimeService"/> — the write-side of the EXIF-derived
/// <c>FileNode.CapturedAtUtc</c> column that drives the "image creation time" shown on photo listings.
/// </summary>
[TestClass]
public class MediaCaptureTimeServiceTests
{
    private string _basePath = null!;
    private LocalFileStorageEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dnc-capture-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(_basePath);
        _engine = new LocalFileStorageEngine(_basePath, NullLogger<LocalFileStorageEngine>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
    }

    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options);

    private MediaCaptureTimeService CreateService(FilesDbContext db) =>
        new(db, _engine, NullLogger<MediaCaptureTimeService>.Instance);

    private static FileNode NewNode(string mimeType = "image/jpeg") => new()
    {
        Name = "photo.jpg",
        NodeType = FileNodeType.File,
        OwnerId = Guid.CreateVersion7(),
        MimeType = mimeType
    };

    private static byte[] JpegWithExifDate(string exifDate)
    {
        using var buffer = new MemoryStream();
        using (var image = new Image<Rgb24>(64, 64))
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.DateTimeOriginal, exifDate);
            image.Metadata.ExifProfile = exif;
            image.SaveAsJpeg(buffer);
        }

        return buffer.ToArray();
    }

    [TestMethod]
    public async Task TrySetCaptureTimeAsync_WhenColumnNull_SetsValueAndReturnsTrue()
    {
        using var db = CreateContext();
        var node = NewNode();
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var captured = new DateTime(2023, 7, 14, 15, 30, 0, DateTimeKind.Utc);
        var result = await CreateService(db).TrySetCaptureTimeAsync(node.Id, captured);

        Assert.IsTrue(result);
        var reloaded = await db.FileNodes.AsNoTracking().SingleAsync(n => n.Id == node.Id);
        Assert.AreEqual(captured, reloaded.CapturedAtUtc);
    }

    [TestMethod]
    public async Task TrySetCaptureTimeAsync_WhenAlreadySet_DoesNotOverwriteAndReturnsFalse()
    {
        using var db = CreateContext();
        var original = new DateTime(2020, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var node = NewNode();
        node.CapturedAtUtc = original;
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .TrySetCaptureTimeAsync(node.Id, new DateTime(2024, 5, 5, 5, 5, 5, DateTimeKind.Utc));

        Assert.IsFalse(result, "An existing capture timestamp must never be overwritten.");
        var reloaded = await db.FileNodes.AsNoTracking().SingleAsync(n => n.Id == node.Id);
        Assert.AreEqual(original, reloaded.CapturedAtUtc);
    }

    [TestMethod]
    public async Task TrySetCaptureTimeAsync_WhenNodeMissing_ReturnsFalse()
    {
        using var db = CreateContext();
        var result = await CreateService(db).TrySetCaptureTimeAsync(Guid.CreateVersion7(), DateTime.UtcNow);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task TrySetCaptureTimeAsync_WhenNodeTrashed_StillSetsValue()
    {
        using var db = CreateContext();
        var node = NewNode();
        node.IsDeleted = true;
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var captured = new DateTime(2022, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var result = await CreateService(db).TrySetCaptureTimeAsync(node.Id, captured);

        Assert.IsTrue(result, "A soft-deleted node still receives its capture time (IgnoreQueryFilters).");
        var reloaded = await db.FileNodes.IgnoreQueryFilters().AsNoTracking().SingleAsync(n => n.Id == node.Id);
        Assert.AreEqual(captured, reloaded.CapturedAtUtc);
    }

    [TestMethod]
    public async Task TryCaptureFromStreamAsync_ImageWithExifDate_RecordsCaptureTime()
    {
        using var db = CreateContext();
        var node = NewNode();
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        using var content = new MemoryStream(JpegWithExifDate("2023:07:14 15:30:00"));
        var result = await CreateService(db).TryCaptureFromStreamAsync(node.Id, content, "image/jpeg");

        Assert.IsTrue(result);
        var reloaded = await db.FileNodes.AsNoTracking().SingleAsync(n => n.Id == node.Id);
        Assert.AreEqual(new DateTime(2023, 7, 14, 15, 30, 0, DateTimeKind.Utc), reloaded.CapturedAtUtc);
    }

    [TestMethod]
    public async Task TryCaptureFromStreamAsync_NonImageMimeType_ReturnsFalse()
    {
        using var db = CreateContext();
        var node = NewNode("application/pdf");
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        using var content = new MemoryStream([1, 2, 3]);
        var result = await CreateService(db).TryCaptureFromStreamAsync(node.Id, content, "application/pdf");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task TryCaptureFromStorageAsync_WholeFileBlobWithExif_RecordsCaptureTime()
    {
        using var db = CreateContext();
        const string storagePath = "files/ab/cd/exifblob";
        await _engine.WriteChunkAsync(storagePath, JpegWithExifDate("2024:02:03 04:05:06"));

        var node = NewNode();
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TryCaptureFromStorageAsync(node.Id, storagePath, "image/jpeg");

        Assert.IsTrue(result);
        var reloaded = await db.FileNodes.AsNoTracking().SingleAsync(n => n.Id == node.Id);
        Assert.AreEqual(new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc), reloaded.CapturedAtUtc);
    }

    [TestMethod]
    public async Task TryCaptureFromStorageAsync_MissingBlob_ReturnsFalse()
    {
        using var db = CreateContext();
        var node = NewNode();
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TryCaptureFromStorageAsync(node.Id, "files/zz/zz/missing", "image/jpeg");

        Assert.IsFalse(result, "Chunked media with no whole-file blob is skipped.");
        var reloaded = await db.FileNodes.AsNoTracking().SingleAsync(n => n.Id == node.Id);
        Assert.IsNull(reloaded.CapturedAtUtc);
    }
}
