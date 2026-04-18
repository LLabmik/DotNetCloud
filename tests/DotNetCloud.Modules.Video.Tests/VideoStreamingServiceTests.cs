using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoStreamingServiceTests
{
    private VideoDbContext _db = null!;
    private VideoStreamingService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new VideoStreamingService(_db, Mock.Of<ILogger<VideoStreamingService>>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task GetVideoForStreamingAsync_ReturnsVideo_WhenOwned()
    {
        var userId = Guid.NewGuid();
        var video = await TestHelpers.SeedVideoAsync(_db, "Stream Video", ownerId: userId);

        var result = await _service.GetVideoForStreamingAsync(video.Id, userId);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetVideoForStreamingAsync_ReturnsNull_WhenNotOwned()
    {
        var video = await TestHelpers.SeedVideoAsync(_db, "Not Mine");

        var result = await _service.GetVideoForStreamingAsync(video.Id, Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GenerateStreamToken_ReturnsNonEmptyToken()
    {
        var token = _service.GenerateStreamToken(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsFalse(string.IsNullOrEmpty(token));
    }

    [TestMethod]
    public void ValidateStreamToken_ReturnsTokenInfo_WhenValid()
    {
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var token = _service.GenerateStreamToken(videoId, userId);

        var result = _service.ValidateStreamToken(token);

        Assert.IsNotNull(result);
        Assert.AreEqual(videoId, result.VideoId);
        Assert.AreEqual(userId, result.UserId);
    }

    [TestMethod]
    public void ValidateStreamToken_ReturnsNull_ForUnknownToken()
    {
        var result = _service.ValidateStreamToken("unknown-token");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ValidateStreamToken_ReturnsNull_ForExpiredToken()
    {
        _service.StreamTokenLifetime = TimeSpan.FromMilliseconds(1);
        var token = _service.GenerateStreamToken(Guid.NewGuid(), Guid.NewGuid());

        Thread.Sleep(10); // Let it expire

        var result = _service.ValidateStreamToken(token);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void AcquireStreamSlot_Succeeds_WhenUnderLimit()
    {
        var userId = Guid.NewGuid();

        _service.AcquireStreamSlot(userId);

        Assert.AreEqual(1, _service.GetActiveStreamCount(userId));
    }

    [TestMethod]
    public void AcquireStreamSlot_ThrowsWhenLimitExceeded()
    {
        var userId = Guid.NewGuid();
        _service.MaxConcurrentStreams = 2;
        _service.AcquireStreamSlot(userId);
        _service.AcquireStreamSlot(userId);

        var ex = Assert.ThrowsExactly<BusinessRuleException>(
            () => _service.AcquireStreamSlot(userId));

        Assert.AreEqual(ErrorCodes.VideoStreamLimitExceeded, ex.ErrorCode);
    }

    [TestMethod]
    public void ReleaseStreamSlot_DecrementsCount()
    {
        var userId = Guid.NewGuid();
        _service.AcquireStreamSlot(userId);
        _service.AcquireStreamSlot(userId);

        _service.ReleaseStreamSlot(userId);

        Assert.AreEqual(1, _service.GetActiveStreamCount(userId));
    }

    [TestMethod]
    public void ReleaseStreamSlot_DoesNotGoNegative()
    {
        var userId = Guid.NewGuid();

        _service.ReleaseStreamSlot(userId);

        Assert.AreEqual(0, _service.GetActiveStreamCount(userId));
    }

    [TestMethod]
    public void GetActiveStreamCount_ReturnsZero_WhenNoActiveStreams()
    {
        Assert.AreEqual(0, _service.GetActiveStreamCount(Guid.NewGuid()));
    }

    // ─── ParseRangeHeader Tests ─────────────────────────────────────

    [TestMethod]
    public void ParseRangeHeader_ReturnsNull_WhenNull()
    {
        Assert.IsNull(VideoStreamingService.ParseRangeHeader(null, 1000));
    }

    [TestMethod]
    public void ParseRangeHeader_ReturnsNull_WhenEmpty()
    {
        Assert.IsNull(VideoStreamingService.ParseRangeHeader("", 1000));
    }

    [TestMethod]
    public void ParseRangeHeader_ReturnsNull_WhenInvalidPrefix()
    {
        Assert.IsNull(VideoStreamingService.ParseRangeHeader("items=0-99", 1000));
    }

    [TestMethod]
    public void ParseRangeHeader_ParsesStartAndEnd()
    {
        var range = VideoStreamingService.ParseRangeHeader("bytes=0-499", 1000);

        Assert.IsNotNull(range);
        Assert.AreEqual(0, range.Value.Start);
        Assert.AreEqual(499, range.Value.End);
    }

    [TestMethod]
    public void ParseRangeHeader_ParsesStartOnly()
    {
        var range = VideoStreamingService.ParseRangeHeader("bytes=500-", 1000);

        Assert.IsNotNull(range);
        Assert.AreEqual(500, range.Value.Start);
        Assert.AreEqual(999, range.Value.End);
    }

    [TestMethod]
    public void ParseRangeHeader_ParsesSuffixRange()
    {
        var range = VideoStreamingService.ParseRangeHeader("bytes=-200", 1000);

        Assert.IsNotNull(range);
        Assert.AreEqual(800, range.Value.Start);
        Assert.AreEqual(999, range.Value.End);
    }

    [TestMethod]
    public void ParseRangeHeader_ClampsEndToLength()
    {
        var range = VideoStreamingService.ParseRangeHeader("bytes=0-9999", 1000);

        Assert.IsNotNull(range);
        Assert.AreEqual(0, range.Value.Start);
        Assert.AreEqual(999, range.Value.End);
    }

    // ─── GetContentType Tests ───────────────────────────────────────

    [TestMethod]
    public void GetContentType_ReturnsMp4ForMp4()
    {
        Assert.AreEqual("video/mp4", VideoStreamingService.GetContentType("video/mp4"));
    }

    [TestMethod]
    public void GetContentType_ReturnsMp4ForQuicktime()
    {
        Assert.AreEqual("video/mp4", VideoStreamingService.GetContentType("video/quicktime"));
    }

    [TestMethod]
    public void GetContentType_ReturnsFallbackForUnknown()
    {
        Assert.AreEqual("application/octet-stream", VideoStreamingService.GetContentType("video/unknown-format"));
    }
}
