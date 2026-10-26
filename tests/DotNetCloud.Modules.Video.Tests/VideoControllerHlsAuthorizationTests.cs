using System.Security.Claims;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Host.Controllers;
using DotNetCloud.Modules.Video.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

/// <summary>
/// Regression tests for the HLS segment/playlist authorization gate on
/// <see cref="VideoController.GetHlsSegment"/>.
/// </summary>
/// <remarks>
/// Finding <c>authz.video.hls-segment.no-video-entitlement</c>: the route is <c>[AllowAnonymous]</c>
/// (the HLS player cannot send an antiforgery header), so authentication alone proved nothing. The
/// endpoint must resolve the video through the owner-scoped
/// <see cref="VideoStreamingService.GetVideoForStreamingAsync"/> before it touches the HLS job, in
/// <b>both</b> the stream-token and the session-cookie branch. These tests assert that a non-owner
/// gets the same 404 as a missing stream even while the owner has a live job and the segment exists
/// on disk — i.e. the job lookup is never reached for a non-entitled caller.
/// </remarks>
[TestClass]
public class VideoControllerHlsAuthorizationTests
{
    private const string PlaylistName = "playlist.m3u8";
    private const string SegmentName = "segment_00000.ts";

    private VideoDbContext _db = null!;
    private VideoStreamingService _streaming = null!;
    private Mock<IVideoTranscodingService> _transcoding = null!;
    private string _hlsDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _streaming = new VideoStreamingService(_db, Mock.Of<ILogger<VideoStreamingService>>());
        _transcoding = new Mock<IVideoTranscodingService>();
        _hlsDir = Path.Combine(
            Path.GetTempPath(), "dnc-video-hls-tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(_hlsDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();

        if (Directory.Exists(_hlsDir))
            Directory.Delete(_hlsDir, recursive: true);
    }

    [TestMethod]
    public async Task GetHlsSegment_StreamTokenForOtherUsersVideo_ReturnsNotFoundWithoutTouchingHlsJob()
    {
        var ownerId = Guid.CreateVersion7();
        var attackerId = Guid.CreateVersion7();
        var video = await TestHelpers.SeedVideoAsync(_db, "Owned By A", ownerId: ownerId);
        CreateHlsJobWithFiles(video.Id, ownerId);

        // A token is genuine and unexpired, but it was minted for a different user than the video owner.
        var attackerToken = _streaming.GenerateStreamToken(video.Id, attackerId);
        var controller = CreateController();

        var result = await controller.GetHlsSegment(video.Id, PlaylistName, attackerToken);

        AssertNotFound(result);
        _transcoding.Verify(t => t.GetActiveHlsJob(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task GetHlsSegment_SessionCookieForOtherUsersVideo_ReturnsNotFoundWithoutTouchingHlsJob()
    {
        var ownerId = Guid.CreateVersion7();
        var attackerId = Guid.CreateVersion7();
        var video = await TestHelpers.SeedVideoAsync(_db, "Owned By A", ownerId: ownerId);
        CreateHlsJobWithFiles(video.Id, ownerId);

        var controller = CreateController(authenticatedUserId: attackerId);

        var result = await controller.GetHlsSegment(video.Id, SegmentName, token: null);

        AssertNotFound(result);
        _transcoding.Verify(t => t.GetActiveHlsJob(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task GetHlsSegment_StreamTokenForOwner_ServesPlaylist()
    {
        var ownerId = Guid.CreateVersion7();
        var video = await TestHelpers.SeedVideoAsync(_db, "Owned By A", ownerId: ownerId);
        var job = CreateHlsJobWithFiles(video.Id, ownerId);

        var ownerToken = _streaming.GenerateStreamToken(video.Id, ownerId);
        var controller = CreateController();

        var result = await controller.GetHlsSegment(video.Id, PlaylistName, ownerToken);

        var fileResult = AssertServed(result, "application/vnd.apple.mpegurl");
        Assert.AreEqual(Path.Combine(_hlsDir, PlaylistName), fileResult.FileName);
        Assert.IsNotNull(job.LastSegmentRequestedAt, "Serving a segment should mark the job as live.");
    }

    [TestMethod]
    public async Task GetHlsSegment_SessionCookieForOwner_ServesSegment()
    {
        var ownerId = Guid.CreateVersion7();
        var video = await TestHelpers.SeedVideoAsync(_db, "Owned By A", ownerId: ownerId);
        CreateHlsJobWithFiles(video.Id, ownerId);

        var controller = CreateController(authenticatedUserId: ownerId);

        var result = await controller.GetHlsSegment(video.Id, SegmentName, token: null);

        var fileResult = AssertServed(result, "video/mp2t");
        Assert.AreEqual(Path.Combine(_hlsDir, SegmentName), fileResult.FileName);
    }

    [TestMethod]
    public async Task GetHlsSegmentFallback_StreamTokenForOtherUsersVideo_ReturnsNotFound()
    {
        var ownerId = Guid.CreateVersion7();
        var attackerId = Guid.CreateVersion7();
        var video = await TestHelpers.SeedVideoAsync(_db, "Owned By A", ownerId: ownerId);
        CreateHlsJobWithFiles(video.Id, ownerId);

        var attackerToken = _streaming.GenerateStreamToken(video.Id, attackerId);
        var controller = CreateController();

        // The browser resolves relative playlist paths against the video URL, so the same files
        // also arrive through the catch-all fallback route. It must not be a bypass.
        var result = await controller.GetHlsSegmentFallback(video.Id, SegmentName, attackerToken);

        AssertNotFound(result);
        _transcoding.Verify(t => t.GetActiveHlsJob(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task GetHlsSegment_UnknownVideo_ReturnsNotFound()
    {
        var userId = Guid.CreateVersion7();
        var controller = CreateController(authenticatedUserId: userId);

        var result = await controller.GetHlsSegment(Guid.CreateVersion7(), SegmentName, token: null);

        AssertNotFound(result);
    }

    [TestMethod]
    public async Task GetHlsSegment_TokenForDifferentVideo_ReturnsForbidden()
    {
        var ownerId = Guid.CreateVersion7();
        var video = await TestHelpers.SeedVideoAsync(_db, "Owned By A", ownerId: ownerId);
        var otherVideo = await TestHelpers.SeedVideoAsync(_db, "Another Video", ownerId: ownerId);
        CreateHlsJobWithFiles(video.Id, ownerId);

        // Token minted for `otherVideo`, replayed against `video`'s segments.
        var mismatchedToken = _streaming.GenerateStreamToken(otherVideo.Id, ownerId);
        var controller = CreateController();

        var result = await controller.GetHlsSegment(video.Id, SegmentName, mismatchedToken);

        Assert.IsInstanceOfType<ForbidResult>(result);
        _transcoding.Verify(t => t.GetActiveHlsJob(It.IsAny<Guid>()), Times.Never);
    }

    private TranscodingJob CreateHlsJobWithFiles(Guid videoId, Guid ownerId)
    {
        File.WriteAllText(Path.Combine(_hlsDir, PlaylistName), "#EXTM3U\n#EXT-X-TARGETDURATION:6\n");
        File.WriteAllText(Path.Combine(_hlsDir, SegmentName), "not-a-real-transport-stream");

        var job = new TranscodingJob
        {
            Id = Guid.CreateVersion7().ToString("N"),
            VideoId = videoId,
            UserId = ownerId,
            CacheKey = $"hls-{videoId:N}",
            OutputPath = Path.Combine(_hlsDir, PlaylistName),
            Status = TranscodingJobStatus.Running,
            IsHls = true
        };

        _transcoding.Setup(t => t.GetActiveHlsJob(videoId)).Returns(job);
        return job;
    }

    /// <summary>
    /// Minimal harness: only the dependencies reached by the HLS segment route are supplied.
    /// The remaining constructor parameters belong to unrelated endpoints and are never dereferenced.
    /// </summary>
    private VideoController CreateController(Guid? authenticatedUserId = null)
    {
        var controller = new VideoController(
            videoService: null!,
            collectionService: null!,
            subtitleService: null!,
            streamingService: _streaming,
            metadataService: null!,
            downloadService: Mock.Of<IDownloadService>(),
            thumbnailService: Mock.Of<IVideoThumbnailService>(),
            enrichmentService: Mock.Of<IVideoEnrichmentService>(),
            transcodingService: _transcoding.Object,
            watchProgressService: Mock.Of<IWatchProgressService>(),
            streamProgress: new StreamProgressState(),
            logger: Mock.Of<ILogger<VideoController>>());

        var httpContext = new DefaultHttpContext();

        if (authenticatedUserId is { } userId)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, "user")
                ],
                authenticationType: "Identity.Application"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static void AssertNotFound(IActionResult result)
    {
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    private static PhysicalFileResult AssertServed(IActionResult result, string expectedContentType)
    {
        var fileResult = Assert.IsInstanceOfType<PhysicalFileResult>(result);
        Assert.AreEqual(expectedContentType, fileResult.ContentType);
        return fileResult;
    }
}
