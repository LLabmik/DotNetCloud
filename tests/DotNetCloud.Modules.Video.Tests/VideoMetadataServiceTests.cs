using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoMetadataServiceTests
{
    private VideoDbContext _db = null!;
    private VideoMetadataService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new VideoMetadataService(_db, Mock.Of<ILogger<VideoMetadataService>>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task GetMetadataAsync_ReturnsMetadata_WhenExists()
    {
        var caller = TestHelpers.CreateCaller();
        var (video, _) = await TestHelpers.SeedCompleteVideoAsync(_db, ownerId: caller.UserId);

        var result = await _service.GetMetadataAsync(video.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(1920, result.Width);
        Assert.AreEqual(1080, result.Height);
        Assert.AreEqual("H.264", result.VideoCodec);
        Assert.AreEqual("AAC", result.AudioCodec);
    }

    [TestMethod]
    public async Task GetMetadataAsync_ReturnsNull_WhenNotExists()
    {
        var result = await _service.GetMetadataAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SaveMetadataAsync_CreatesNewMetadata()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        var metadata = new VideoMetadata
        {
            VideoId = video.Id,
            Width = 3840,
            Height = 2160,
            FrameRate = 60.0,
            VideoCodec = "H.265",
            AudioCodec = "Opus",
            Bitrate = 20_000_000,
            AudioTrackCount = 1,
            SubtitleTrackCount = 0,
            ContainerFormat = "MKV"
        };

        await _service.SaveMetadataAsync(video.Id, metadata);

        var result = await _service.GetMetadataAsync(video.Id);
        Assert.IsNotNull(result);
        Assert.AreEqual(3840, result.Width);
        Assert.AreEqual(2160, result.Height);
        Assert.AreEqual("H.265", result.VideoCodec);
    }

    [TestMethod]
    public async Task SaveMetadataAsync_UpdatesExistingMetadata()
    {
        var caller = TestHelpers.CreateCaller();
        var (video, _) = await TestHelpers.SeedCompleteVideoAsync(_db, ownerId: caller.UserId);
        var metadata = new VideoMetadata
        {
            VideoId = video.Id,
            Width = 3840,
            Height = 2160,
            FrameRate = 30.0,
            VideoCodec = "AV1",
            AudioCodec = "Opus",
            Bitrate = 15_000_000,
            AudioTrackCount = 2,
            SubtitleTrackCount = 3,
            ContainerFormat = "WebM"
        };

        await _service.SaveMetadataAsync(video.Id, metadata);

        var result = await _service.GetMetadataAsync(video.Id);
        Assert.IsNotNull(result);
        Assert.AreEqual(3840, result.Width);
        Assert.AreEqual("AV1", result.VideoCodec);
        Assert.AreEqual("WebM", result.ContainerFormat);
    }

    [TestMethod]
    public async Task SaveMetadataAsync_SavesEvenForNonExistentVideo()
    {
        var nonExistentVideoId = Guid.NewGuid();
        var metadata = new VideoMetadata
        {
            VideoId = nonExistentVideoId,
            Width = 1280,
            Height = 720
        };

        await _service.SaveMetadataAsync(nonExistentVideoId, metadata);

        var result = await _service.GetMetadataAsync(nonExistentVideoId);
        Assert.IsNotNull(result);
        Assert.AreEqual(1280, result.Width);
    }
}
