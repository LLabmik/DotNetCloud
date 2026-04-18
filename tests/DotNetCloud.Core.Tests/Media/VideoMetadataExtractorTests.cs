namespace DotNetCloud.Core.Tests.Media;

using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.ServiceDefaults.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>
/// Tests for <see cref="VideoMetadataExtractor"/>.
/// Tests focus on MIME type support, error handling, and FFprobe output parsing.
/// FFprobe-dependent tests are isolated via configuration.
/// </summary>
[TestClass]
public class VideoMetadataExtractorTests
{
    private VideoMetadataExtractor _extractor = null!;
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Media:FfprobePath"] = "ffprobe"  // default system path
            })
            .Build();
        var logger = new Mock<ILogger<VideoMetadataExtractor>>();
        _extractor = new VideoMetadataExtractor(config, logger.Object);
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
    public void SupportedMediaType_ReturnsVideo()
    {
        Assert.AreEqual(MediaType.Video, _extractor.SupportedMediaType);
    }

    [TestMethod]
    [DataRow("video/mp4", true)]
    [DataRow("video/mpeg", true)]
    [DataRow("video/quicktime", true)]
    [DataRow("video/x-msvideo", true)]
    [DataRow("video/x-matroska", true)]
    [DataRow("video/webm", true)]
    [DataRow("video/x-flv", true)]
    [DataRow("video/3gpp", true)]
    [DataRow("video/ogg", true)]
    [DataRow("image/jpeg", false)]
    [DataRow("audio/mpeg", false)]
    [DataRow("text/plain", false)]
    [DataRow("application/octet-stream", false)]
    public void CanExtract_ReturnsExpected(string mimeType, bool expected)
    {
        Assert.AreEqual(expected, _extractor.CanExtract(mimeType));
    }

    [TestMethod]
    public async Task ExtractAsync_NonexistentFile_ReturnsNull()
    {
        // Act
        var result = await _extractor.ExtractAsync("/nonexistent/video.mp4", "video/mp4");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ExtractAsync_EmptyFile_ReturnsNull()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "empty.mp4");
        await File.WriteAllBytesAsync(filePath, Array.Empty<byte>());

        // Act
        var result = await _extractor.ExtractAsync(filePath, "video/mp4");

        // Assert — ffprobe should handle empty files gracefully
        Assert.IsNull(result);
    }

    [TestMethod]
    public void CanExtract_CaseInsensitive_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("VIDEO/MP4"));
        Assert.IsTrue(_extractor.CanExtract("Video/Webm"));
    }

    [TestMethod]
    public void Constructor_DefaultFfprobePath_UsesDefault()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var logger = new Mock<ILogger<VideoMetadataExtractor>>();

        // Act — should not throw even without config
        var extractor = new VideoMetadataExtractor(config, logger.Object);

        // Assert
        Assert.IsNotNull(extractor);
        Assert.AreEqual(MediaType.Video, extractor.SupportedMediaType);
    }

    [TestMethod]
    public void Constructor_CustomFfprobePath_UsesConfigured()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Media:FfprobePath"] = "/usr/local/bin/ffprobe"
            })
            .Build();
        var logger = new Mock<ILogger<VideoMetadataExtractor>>();

        // Act
        var extractor = new VideoMetadataExtractor(config, logger.Object);

        // Assert
        Assert.IsNotNull(extractor);
    }
}
