namespace DotNetCloud.Core.Tests.Middleware;

using DotNetCloud.Core.ServiceDefaults.Middleware;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="MediaStreamingMiddleware"/> range-header parsing and streaming logic.
/// </summary>
[TestClass]
public class MediaStreamingMiddlewareTests
{
    // ── ParseRangeHeader Tests ──────────────────────────────────────────

    [TestMethod]
    public void ParseRangeHeader_Null_ReturnsNulls()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader(null, 1000);

        // Assert
        Assert.IsNull(start);
        Assert.IsNull(end);
    }

    [TestMethod]
    public void ParseRangeHeader_Empty_ReturnsNulls()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("", 1000);

        // Assert
        Assert.IsNull(start);
        Assert.IsNull(end);
    }

    [TestMethod]
    public void ParseRangeHeader_Whitespace_ReturnsNulls()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("   ", 1000);

        // Assert
        Assert.IsNull(start);
        Assert.IsNull(end);
    }

    [TestMethod]
    public void ParseRangeHeader_InvalidPrefix_ReturnsNulls()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("chars=0-100", 1000);

        // Assert
        Assert.IsNull(start);
        Assert.IsNull(end);
    }

    [TestMethod]
    public void ParseRangeHeader_FullRange_ParsesCorrectly()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=0-999", 1000);

        // Assert
        Assert.AreEqual(0L, start);
        Assert.AreEqual(999L, end);
    }

    [TestMethod]
    public void ParseRangeHeader_StartOnly_ParsesStartWithNullEnd()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=500-", 1000);

        // Assert
        Assert.AreEqual(500L, start);
        Assert.IsNull(end);
    }

    [TestMethod]
    public void ParseRangeHeader_SuffixRange_CalculatesFromEnd()
    {
        // Act — "bytes=-200" means the last 200 bytes
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=-200", 1000);

        // Assert
        Assert.AreEqual(800L, start);
        Assert.AreEqual(999L, end);
    }

    [TestMethod]
    public void ParseRangeHeader_SuffixRange_LargerThanFile_ClampsToZero()
    {
        // Act — "bytes=-2000" with a 1000-byte file means start at 0
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=-2000", 1000);

        // Assert
        Assert.AreEqual(0L, start);
        Assert.AreEqual(999L, end);
    }

    [TestMethod]
    public void ParseRangeHeader_PartialRange_ParsesStartAndEnd()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=100-500", 1000);

        // Assert
        Assert.AreEqual(100L, start);
        Assert.AreEqual(500L, end);
    }

    [TestMethod]
    public void ParseRangeHeader_MultiRange_ReturnsNulls()
    {
        // Act — multi-range not supported
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=0-100,200-300", 1000);

        // Assert
        Assert.IsNull(start);
        Assert.IsNull(end);
    }

    [TestMethod]
    public void ParseRangeHeader_NoDash_ReturnsNulls()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=500", 1000);

        // Assert
        Assert.IsNull(start);
        Assert.IsNull(end);
    }

    [TestMethod]
    public void ParseRangeHeader_InvalidStart_ReturnsNulls()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=abc-500", 1000);

        // Assert
        Assert.IsNull(start);
        Assert.IsNull(end);
    }

    [TestMethod]
    public void ParseRangeHeader_InvalidEnd_ReturnsNulls()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=100-abc", 1000);

        // Assert
        Assert.IsNull(start);
        Assert.IsNull(end);
    }

    [TestMethod]
    public void ParseRangeHeader_EndBeforeStart_ReturnsNulls()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=500-100", 1000);

        // Assert
        Assert.IsNull(start);
        Assert.IsNull(end);
    }

    [TestMethod]
    public void ParseRangeHeader_ZeroStart_ParsesCorrectly()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=0-0", 1000);

        // Assert
        Assert.AreEqual(0L, start);
        Assert.AreEqual(0L, end);
    }

    [TestMethod]
    public void ParseRangeHeader_CaseInsensitivePrefix()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("Bytes=0-100", 1000);

        // Assert
        Assert.AreEqual(0L, start);
        Assert.AreEqual(100L, end);
    }

    [TestMethod]
    public void ParseRangeHeader_WithWhitespace_ParsesCorrectly()
    {
        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes= 0 - 100 ", 1000);

        // Assert — whitespace in range spec is trimmed
        Assert.AreEqual(0L, start);
        Assert.AreEqual(100L, end);
    }

    [TestMethod]
    public void ParseRangeHeader_LargeFileSize_HandlesLongValues()
    {
        // Arrange — 10 GB file
        long totalLength = 10_737_418_240L;

        // Act
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=5368709120-10737418239", totalLength);

        // Assert
        Assert.AreEqual(5_368_709_120L, start);
        Assert.AreEqual(10_737_418_239L, end);
    }

    [TestMethod]
    public void ParseRangeHeader_NegativeSuffix_ZeroLength_ReturnsNulls()
    {
        // Act — "bytes=-0" is meaningless
        var (start, end) = MediaStreamingMiddleware.ParseRangeHeader("bytes=-0", 1000);

        // Assert
        Assert.IsNull(start);
        Assert.IsNull(end);
    }

    // ── MediaFileInfo Tests ─────────────────────────────────────────────

    [TestMethod]
    public void MediaFileInfo_RequiredProperties_AreSet()
    {
        // Arrange & Act
        var info = new MediaFileInfo
        {
            ContentType = "video/mp4",
            TotalLength = 52_428_800
        };

        // Assert
        Assert.AreEqual("video/mp4", info.ContentType);
        Assert.AreEqual(52_428_800, info.TotalLength);
        Assert.IsNull(info.FileName);
    }

    [TestMethod]
    public void MediaFileInfo_WithFileName_SetsOptionalField()
    {
        // Arrange & Act
        var info = new MediaFileInfo
        {
            ContentType = "audio/flac",
            TotalLength = 30_000_000,
            FileName = "song.flac"
        };

        // Assert
        Assert.AreEqual("song.flac", info.FileName);
    }
}
