using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class CoverArtArchiveClientTests
{
    private MusicBrainzRateLimiter _rateLimiter = null!;

    [TestInitialize]
    public void Setup()
    {
        _rateLimiter = new MusicBrainzRateLimiter(0);
    }

    [TestCleanup]
    public void Cleanup() => _rateLimiter.Dispose();

    private CoverArtArchiveClient CreateClient(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://coverartarchive.org/") };
        return new CoverArtArchiveClient(httpClient, _rateLimiter, NullLogger<CoverArtArchiveClient>.Instance);
    }

    // ── Successful Fetches ───────────────────────────────────────────

    [TestMethod]
    public async Task GetFrontCover_ValidRelease_ReturnsImageData()
    {
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var handler = MockHttpMessageHandler.ForBytes(imageData, "image/jpeg");
        var client = CreateClient(handler);

        var result = await client.GetFrontCoverAsync("release-123");

        Assert.IsNotNull(result);
        Assert.AreEqual("image/jpeg", result.MimeType);
        CollectionAssert.AreEqual(imageData, result.Data);
        Assert.AreEqual("release-123", result.ReleaseMbid);
    }

    [TestMethod]
    public async Task GetFrontCover_PngImage_ReturnsPngMimeType()
    {
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var handler = MockHttpMessageHandler.ForBytes(imageData, "image/png");
        var client = CreateClient(handler);

        var result = await client.GetFrontCoverAsync("release-456");

        Assert.IsNotNull(result);
        Assert.AreEqual("image/png", result.MimeType);
    }

    [TestMethod]
    public async Task GetCoverList_ReturnsAllImages()
    {
        var json = """
        {
            "images": [
                {"id":12345,"types":["Front"],"front":true,"back":false,"image":"https://archive.org/front.jpg"},
                {"id":12346,"types":["Back"],"front":false,"back":true,"image":"https://archive.org/back.jpg"},
                {"id":12347,"types":["Booklet"],"front":false,"back":false,"image":"https://archive.org/booklet.jpg"}
            ]
        }
        """;
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var images = await client.GetCoverListAsync("release-789");

        Assert.IsNotNull(images);
        Assert.AreEqual(3, images.Count);
        Assert.IsTrue(images[0].Front);
        Assert.IsTrue(images[1].Back);
        Assert.AreEqual("https://archive.org/booklet.jpg", images[2].Image);
    }

    // ── Fallback Logic ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetFrontCoverFromReleases_FirstRelease404_FallsToSecond()
    {
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF };
        var callCount = 0;
        var handler = new MockHttpMessageHandler(request =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(imageData)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return response;
        });
        var client = CreateClient(handler);

        var releases = new List<MusicBrainzRelease>
        {
            new() { Id = "r1", Title = "Release 1" },
            new() { Id = "r2", Title = "Release 2" }
        };

        var result = await client.GetFrontCoverFromReleasesAsync(releases);

        Assert.IsNotNull(result);
        Assert.AreEqual("r2", result.ReleaseMbid);
        CollectionAssert.AreEqual(imageData, result.Data);
    }

    [TestMethod]
    public async Task GetFrontCoverFromReleases_AllReleases404_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForStatus(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        var releases = new List<MusicBrainzRelease>
        {
            new() { Id = "r1", Title = "Release 1" },
            new() { Id = "r2", Title = "Release 2" },
            new() { Id = "r3", Title = "Release 3" }
        };

        var result = await client.GetFrontCoverFromReleasesAsync(releases);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetFrontCoverFromReleases_EmptyReleaseList_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForJson("{}");
        var client = CreateClient(handler);

        var result = await client.GetFrontCoverFromReleasesAsync([]);

        Assert.IsNull(result);
        Assert.AreEqual(0, handler.ReceivedRequests.Count);
    }

    // ── Error Handling ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetFrontCover_Http503_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForStatus(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        var result = await client.GetFrontCoverAsync("release-123");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetFrontCover_NetworkError_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForNetworkError();
        var client = CreateClient(handler);

        var result = await client.GetFrontCoverAsync("release-123");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetFrontCover_Timeout_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForTimeout();
        var client = CreateClient(handler);

        var result = await client.GetFrontCoverAsync("release-123");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetFrontCover_LargeImage_ReturnsData()
    {
        var largeImage = new byte[6 * 1024 * 1024]; // 6 MB
        new Random(42).NextBytes(largeImage);
        var handler = MockHttpMessageHandler.ForBytes(largeImage, "image/jpeg");
        var client = CreateClient(handler);

        var result = await client.GetFrontCoverAsync("release-big");

        Assert.IsNotNull(result);
        Assert.AreEqual(largeImage.Length, result.Data.Length);
    }

    [TestMethod]
    public async Task GetFrontCover_EmptyBody_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForBytes([], "image/jpeg");
        var client = CreateClient(handler);

        var result = await client.GetFrontCoverAsync("release-empty");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetFrontCover_InvalidMimeType_DefaultsToJpeg()
    {
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF };
        var handler = MockHttpMessageHandler.ForBytes(imageData, "application/octet-stream");
        var client = CreateClient(handler);

        var result = await client.GetFrontCoverAsync("release-unknown");

        Assert.IsNotNull(result);
        Assert.AreEqual("image/jpeg", result.MimeType);
    }

    [TestMethod]
    public async Task GetCoverList_NotFound_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForStatus(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        var result = await client.GetCoverListAsync("nonexistent");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetCoverList_NetworkError_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForNetworkError();
        var client = CreateClient(handler);

        var result = await client.GetCoverListAsync("release-123");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetFrontCover_Http404_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForStatus(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        var result = await client.GetFrontCoverAsync("no-art-release");

        Assert.IsNull(result);
    }
}
