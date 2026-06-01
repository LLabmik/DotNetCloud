using System.Net;
using DotNetCloud.Modules.Music.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class AudioDbClientTests
{
    private AudioDbClient CreateClient(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.theaudiodb.com/api/v1/json/123/") };
        return new AudioDbClient(httpClient, NullLogger<AudioDbClient>.Instance);
    }

    // ── Successful Fetches ───────────────────────────────────────────

    [TestMethod]
    public async Task GetArtistArtwork_ValidMbid_ReturnsLogoUrl()
    {
        var json = """
        {
            "artists": [
                {
                    "strArtistLogo": "https://www.theaudiodb.com/images/artists/logo/112024.png",
                    "strArtistThumb": "https://www.theaudiodb.com/images/artists/thumb/112024.jpg",
                    "strArtistBanner": "https://www.theaudiodb.com/images/artists/banner/112024.jpg",
                    "strArtistFanart": "https://www.theaudiodb.com/images/artists/fanart/112024.jpg"
                }
            ]
        }
        """;
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var result = await client.GetArtistArtworkAsync("83d91898-7763-47d7-b03b-faaee372db71");

        Assert.IsNotNull(result);
        Assert.AreEqual("https://www.theaudiodb.com/images/artists/logo/112024.png", result.LogoUrl);
        Assert.AreEqual("https://www.theaudiodb.com/images/artists/thumb/112024.jpg", result.ThumbUrl);
        Assert.AreEqual("https://www.theaudiodb.com/images/artists/banner/112024.jpg", result.BannerUrl);
        Assert.AreEqual("https://www.theaudiodb.com/images/artists/fanart/112024.jpg", result.FanartUrl);
    }

    [TestMethod]
    public async Task GetArtistArtwork_BuildsCorrectUrl()
    {
        var json = """{"artists":[]}""";
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        await client.GetArtistArtworkAsync("test-mbid-123");

        Assert.AreEqual(1, handler.ReceivedRequests.Count);
        var url = handler.ReceivedRequests[0].RequestUri!.ToString();
        Assert.IsTrue(url.Contains("artist-mb.php"), $"URL should contain 'artist-mb.php': {url}");
        Assert.IsTrue(url.Contains("test-mbid-123"), $"URL should contain the MBID: {url}");
    }

    [TestMethod]
    public async Task GetArtistArtwork_NoLogo_ReturnsNullFields()
    {
        var json = """
        {
            "artists": [
                {
                    "strArtistLogo": "",
                    "strArtistThumb": null,
                    "strArtistBanner": "",
                    "strArtistFanart": null
                }
            ]
        }
        """;
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var result = await client.GetArtistArtworkAsync("some-mbid");

        Assert.IsNotNull(result);
        Assert.IsNull(result.LogoUrl);
        Assert.IsNull(result.ThumbUrl);
        Assert.IsNull(result.BannerUrl);
        Assert.IsNull(result.FanartUrl);
    }

    // ── Empty / Not Found ────────────────────────────────────────────

    [TestMethod]
    public async Task GetArtistArtwork_NoArtists_ReturnsNull()
    {
        var json = """{"artists":[]}""";
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var result = await client.GetArtistArtworkAsync("unknown-mbid");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetArtistArtwork_ArtistsNull_ReturnsNull()
    {
        var json = """{}""";
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var result = await client.GetArtistArtworkAsync("some-mbid");

        Assert.IsNull(result);
    }

    // ── Error Handling ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetArtistArtwork_HttpError_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForStatus(HttpStatusCode.InternalServerError);
        var client = CreateClient(handler);

        var result = await client.GetArtistArtworkAsync("some-mbid");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetArtistArtwork_InvalidJson_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForJson("not valid json");
        var client = CreateClient(handler);

        var result = await client.GetArtistArtworkAsync("some-mbid");

        Assert.IsNull(result);
    }
}
