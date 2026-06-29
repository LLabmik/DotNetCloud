using System.Net;
using DotNetCloud.Client.Android.Services;
using Moq;
using Moq.Protected;

namespace DotNetCloud.Client.Android.Tests.Services;

[TestClass]
public sealed class AlbumArtCacheTests
{
    private const string ServerUrl = "https://example.com:15443";
    private const string AccessToken = "test-token";

    private Mock<HttpMessageHandler> _handler = null!;
    private HttpClient _httpClient = null!;

    [TestInitialize]
    public void Setup()
    {
        _handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        _httpClient = new HttpClient(_handler.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _httpClient.Dispose();
    }

    private static string GetUrl(HttpRequestMessage m) =>
        m.RequestUri?.ToString() ?? string.Empty;

    [TestMethod]
    public async Task GetAlbumArtAsync_DownloadsAndReturnsImage_OnCacheMiss()
    {
        var albumId = Guid.NewGuid();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get &&
                    GetUrl(m).Contains($"/api/v1/music/albums/{albumId}/cover")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
            });

        var cache = new AlbumArtCache(_httpClient);
        var result = await cache.GetAlbumArtAsync(albumId, ServerUrl, AccessToken);

        Assert.IsNotNull(result);
        _handler.Protected().Verify(
            "SendAsync", Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task GetAlbumArtAsync_ReturnsCached_OnSecondCall()
    {
        var albumId = Guid.NewGuid();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
            });

        var cache = new AlbumArtCache(_httpClient);

        var first = await cache.GetAlbumArtAsync(albumId, ServerUrl, AccessToken);
        Assert.IsNotNull(first);

        var second = await cache.GetAlbumArtAsync(albumId, ServerUrl, AccessToken);
        Assert.IsNotNull(second);

        _handler.Protected().Verify(
            "SendAsync", Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task GetAlbumArtAsync_ReturnsNull_OnHttpError()
    {
        var albumId = Guid.NewGuid();

        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var cache = new AlbumArtCache(_httpClient);
        var result = await cache.GetAlbumArtAsync(albumId, ServerUrl, AccessToken);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Invalidate_RemovesFromCache()
    {
        var albumId = Guid.NewGuid();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
            });

        var cache = new AlbumArtCache(_httpClient);

        cache.GetAlbumArtAsync(albumId, ServerUrl, AccessToken).GetAwaiter().GetResult();
        cache.Invalidate(albumId);
        cache.GetAlbumArtAsync(albumId, ServerUrl, AccessToken).GetAwaiter().GetResult();

        _handler.Protected().Verify(
            "SendAsync", Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public void Clear_EmptiesAllCaches()
    {
        var albumId = Guid.NewGuid();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
            });

        var cache = new AlbumArtCache(_httpClient);

        cache.GetAlbumArtAsync(albumId, ServerUrl, AccessToken).GetAwaiter().GetResult();
        cache.Clear();
        cache.GetAlbumArtAsync(albumId, ServerUrl, AccessToken).GetAwaiter().GetResult();

        _handler.Protected().Verify(
            "SendAsync", Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public void Invalidate_NonExistent_DoesNotThrow()
    {
        var cache = new AlbumArtCache(_httpClient);
        cache.Invalidate(Guid.NewGuid());
    }

    [TestMethod]
    public void Clear_Empty_DoesNotThrow()
    {
        var cache = new AlbumArtCache(_httpClient);
        cache.Clear();
    }
}
