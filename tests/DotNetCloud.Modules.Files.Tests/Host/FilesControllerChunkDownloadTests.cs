using System.Security.Claims;
using DotNetCloud.Modules.Files.Host.Controllers;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Host;

/// <summary>
/// Unit tests for <see cref="FilesController.DownloadChunkByHashAsync"/> ETag / If-None-Match
/// conditional request behaviour (Task 2.6).
/// </summary>
[TestClass]
public class FilesControllerChunkDownloadTests
{
    private const string SampleHash = "abc123def456";
    private static readonly string ExpectedETag = $"\"{SampleHash}\"";

    private static (FilesController controller, Mock<IDownloadService> downloadMock) CreateController(
        string? ifNoneMatchHeader = null)
    {
        var userId = Guid.NewGuid();

        var downloadMock = new Mock<IDownloadService>();

        var controller = new FilesController(
            Mock.Of<IFileService>(),
            Mock.Of<IChunkedUploadService>(),
            downloadMock.Object,
            Mock.Of<IVersionService>(),
            Mock.Of<IShareService>(),
            Mock.Of<IThumbnailService>(),
            NullLogger<FilesController>.Instance,
            Microsoft.Extensions.Options.Options.Create(new DotNetCloud.Modules.Files.Options.FileSystemOptions()));

        // Set up a minimal authenticated HttpContext
        var httpContext = new DefaultHttpContext();

        if (ifNoneMatchHeader is not null)
            httpContext.Request.Headers.IfNoneMatch = ifNoneMatchHeader;

        // Provide a valid authenticated identity so GetAuthenticatedCaller() succeeds
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        ], authenticationType: "Test");
        httpContext.User = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return (controller, downloadMock);
    }

    [TestMethod]
    public async Task DownloadChunkByHashAsync_IfNoneMatchMatches_Returns304()
    {
        var (controller, _) = CreateController(ifNoneMatchHeader: ExpectedETag);

        var result = await controller.DownloadChunkByHashAsync(SampleHash) as StatusCodeResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(304, result.StatusCode);
    }

    [TestMethod]
    public async Task DownloadChunkByHashAsync_IfNoneMatchWildcard_Returns304()
    {
        var (controller, _) = CreateController(ifNoneMatchHeader: "*");

        var result = await controller.DownloadChunkByHashAsync(SampleHash) as StatusCodeResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(304, result.StatusCode);
    }

    [TestMethod]
    public async Task DownloadChunkByHashAsync_NoIfNoneMatch_ReturnsFileWithETag()
    {
        var (controller, downloadMock) = CreateController(ifNoneMatchHeader: null);
        var fakeStream = new MemoryStream([1, 2, 3]);
        downloadMock
            .Setup(s => s.DownloadChunkByHashAsync(SampleHash, It.IsAny<Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeStream);

        var result = await controller.DownloadChunkByHashAsync(SampleHash);

        // Should be a FileStreamResult (200 OK with a body)
        Assert.IsInstanceOfType<FileStreamResult>(result);
        Assert.AreEqual(ExpectedETag, controller.Response.Headers.ETag.ToString());
        Assert.IsTrue(controller.Response.Headers.CacheControl.ToString().Contains("immutable"));
    }

    [TestMethod]
    public async Task DownloadChunkByHashAsync_DifferentIfNoneMatch_ReturnsFileWithETag()
    {
        var differentETag = "\"otherhash\"";
        var (controller, downloadMock) = CreateController(ifNoneMatchHeader: differentETag);
        var fakeStream = new MemoryStream([4, 5, 6]);
        downloadMock
            .Setup(s => s.DownloadChunkByHashAsync(SampleHash, It.IsAny<Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeStream);

        var result = await controller.DownloadChunkByHashAsync(SampleHash);

        // ETag mismatch — full download should proceed
        Assert.IsInstanceOfType<FileStreamResult>(result);
        Assert.AreEqual(ExpectedETag, controller.Response.Headers.ETag.ToString());
    }

    [TestMethod]
    public async Task DownloadChunkByHashAsync_ChunkNotFound_Returns404()
    {
        var (controller, downloadMock) = CreateController(ifNoneMatchHeader: null);
        downloadMock
            .Setup(s => s.DownloadChunkByHashAsync(SampleHash, It.IsAny<Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var result = await controller.DownloadChunkByHashAsync(SampleHash) as NotFoundObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(404, result.StatusCode);
    }
}
