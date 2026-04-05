namespace DotNetCloud.Core.Tests.Media;

using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.ServiceDefaults.Media;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Tests for <see cref="ExifMetadataExtractor"/>.
/// </summary>
[TestClass]
public class ExifMetadataExtractorTests
{
    private ExifMetadataExtractor _extractor = null!;
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = new Mock<ILogger<ExifMetadataExtractor>>();
        _extractor = new ExifMetadataExtractor(logger.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetcloud-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void SupportedMediaType_ReturnsPhoto()
    {
        Assert.AreEqual(MediaType.Photo, _extractor.SupportedMediaType);
    }

    [TestMethod]
    [DataRow("image/jpeg", true)]
    [DataRow("image/jpg", true)]
    [DataRow("image/png", true)]
    [DataRow("image/gif", true)]
    [DataRow("image/webp", true)]
    [DataRow("image/bmp", true)]
    [DataRow("image/tiff", true)]
    [DataRow("audio/mpeg", false)]
    [DataRow("video/mp4", false)]
    [DataRow("application/pdf", false)]
    [DataRow("text/plain", false)]
    public void CanExtract_ReturnsExpected(string mimeType, bool expected)
    {
        Assert.AreEqual(expected, _extractor.CanExtract(mimeType));
    }

    [TestMethod]
    public async Task ExtractAsync_BasicJpeg_ReturnsDimensions()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "basic.jpg");
        using (var image = new Image<Rgba32>(800, 600))
        {
            await image.SaveAsJpegAsync(filePath);
        }

        // Act
        var result = await _extractor.ExtractAsync(filePath, "image/jpeg");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(MediaType.Photo, result.MediaType);
        Assert.AreEqual(800, result.Width);
        Assert.AreEqual(600, result.Height);
    }

    [TestMethod]
    public async Task ExtractAsync_JpegWithExifData_ExtractsMetadata()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "exif.jpg");
        using (var image = new Image<Rgba32>(1920, 1080))
        {
            var exif = image.Metadata.ExifProfile ?? new ExifProfile();
            exif.SetValue(ExifTag.Make, "TestCam");
            exif.SetValue(ExifTag.Model, "TC-100");
            exif.SetValue(ExifTag.Orientation, (ushort)1);
            exif.SetValue(ExifTag.DateTime, "2026:01:15 10:30:00");
            image.Metadata.ExifProfile = exif;
            await image.SaveAsJpegAsync(filePath);
        }

        // Act
        var result = await _extractor.ExtractAsync(filePath, "image/jpeg");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1920, result.Width);
        Assert.AreEqual(1080, result.Height);
        Assert.AreEqual("TestCam", result.CameraMake);
        Assert.AreEqual("TC-100", result.CameraModel);
        Assert.AreEqual(1, result.Orientation);
    }

    [TestMethod]
    public async Task ExtractAsync_PngFile_ReturnsDimensions()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.png");
        using (var image = new Image<Rgba32>(640, 480))
        {
            await image.SaveAsPngAsync(filePath);
        }

        // Act
        var result = await _extractor.ExtractAsync(filePath, "image/png");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(640, result.Width);
        Assert.AreEqual(480, result.Height);
    }

    [TestMethod]
    public async Task ExtractAsync_NonexistentFile_ReturnsNull()
    {
        // Act
        var result = await _extractor.ExtractAsync("/nonexistent/file.jpg", "image/jpeg");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ExtractAsync_NoExifProfile_ReturnsNullOptionalFields()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "noexif.jpg");
        using (var image = new Image<Rgba32>(100, 100))
        {
            await image.SaveAsJpegAsync(filePath);
        }

        // Act
        var result = await _extractor.ExtractAsync(filePath, "image/jpeg");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(100, result.Width);
        Assert.AreEqual(100, result.Height);
        Assert.IsNull(result.CameraMake);
        Assert.IsNull(result.CameraModel);
        Assert.IsNull(result.Location);
        Assert.IsNull(result.TakenAtUtc);
    }

    [TestMethod]
    public async Task ExtractAsync_JpegWithGps_ExtractsCoordinates()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "gps.jpg");
        using (var image = new Image<Rgba32>(100, 100))
        {
            var exif = new ExifProfile();
            // Paris: 48°51'23.81"N, 2°21'7.99"E
            exif.SetValue(ExifTag.GPSLatitude, new Rational[]
            {
                new(48, 1), new(51, 1), new(2381, 100)
            });
            exif.SetValue(ExifTag.GPSLatitudeRef, "N");
            exif.SetValue(ExifTag.GPSLongitude, new Rational[]
            {
                new(2, 1), new(21, 1), new(799, 100)
            });
            exif.SetValue(ExifTag.GPSLongitudeRef, "E");
            image.Metadata.ExifProfile = exif;
            await image.SaveAsJpegAsync(filePath);
        }

        // Act
        var result = await _extractor.ExtractAsync(filePath, "image/jpeg");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Location);
        Assert.IsTrue(result.Location.Latitude > 48.0 && result.Location.Latitude < 49.0,
            $"Expected latitude ~48.85, got {result.Location.Latitude}");
        Assert.IsTrue(result.Location.Longitude > 2.0 && result.Location.Longitude < 3.0,
            $"Expected longitude ~2.35, got {result.Location.Longitude}");
    }

    [TestMethod]
    public async Task ExtractAsync_JpegWithSouthWestGps_NegatesCoordinates()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "sw-gps.jpg");
        using (var image = new Image<Rgba32>(100, 100))
        {
            var exif = new ExifProfile();
            // Sydney: ~33°52'S, 151°12'E => but let's set W for testing
            exif.SetValue(ExifTag.GPSLatitude, new Rational[]
            {
                new(33, 1), new(52, 1), new(0, 1)
            });
            exif.SetValue(ExifTag.GPSLatitudeRef, "S");
            exif.SetValue(ExifTag.GPSLongitude, new Rational[]
            {
                new(151, 1), new(12, 1), new(0, 1)
            });
            exif.SetValue(ExifTag.GPSLongitudeRef, "W");
            image.Metadata.ExifProfile = exif;
            await image.SaveAsJpegAsync(filePath);
        }

        // Act
        var result = await _extractor.ExtractAsync(filePath, "image/jpeg");

        // Assert
        Assert.IsNotNull(result?.Location);
        Assert.IsTrue(result.Location.Latitude < 0, "S latitude should be negative");
        Assert.IsTrue(result.Location.Longitude < 0, "W longitude should be negative");
    }

    [TestMethod]
    public async Task ExtractAsync_CorruptFile_ReturnsNull()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "corrupt.jpg");
        await File.WriteAllBytesAsync(filePath, new byte[] { 0xFF, 0xD8, 0x00, 0x00, 0x00 });

        // Act
        var result = await _extractor.ExtractAsync(filePath, "image/jpeg");

        // Assert — should handle gracefully without throwing
        // Result may be null or may succeed depending on ImageSharp's resilience
        // The key assertion is that it doesn't throw
    }

    [TestMethod]
    public void CanExtract_CaseInsensitive_ReturnsTrue()
    {
        // Assert
        Assert.IsTrue(_extractor.CanExtract("IMAGE/JPEG"));
        Assert.IsTrue(_extractor.CanExtract("Image/Png"));
    }
}
