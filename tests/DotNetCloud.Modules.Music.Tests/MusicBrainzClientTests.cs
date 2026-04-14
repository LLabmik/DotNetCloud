using System.Net;
using System.Text;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class MusicBrainzClientTests
{
    private MusicBrainzRateLimiter _rateLimiter = null!;

    [TestInitialize]
    public void Setup()
    {
        // Use minimal delay for fast tests
        _rateLimiter = new MusicBrainzRateLimiter(0);
    }

    [TestCleanup]
    public void Cleanup() => _rateLimiter.Dispose();

    private MusicBrainzClient CreateClient(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://musicbrainz.org/ws/2/") };
        return new MusicBrainzClient(httpClient, _rateLimiter, NullLogger<MusicBrainzClient>.Instance);
    }

    // ── URL Construction & Request Format ────────────────────────────

    [TestMethod]
    public async Task SearchArtist_BuildsCorrectUrl()
    {
        var handler = MockHttpMessageHandler.ForJson("""{"artists":[]}""");
        var client = CreateClient(handler);

        await client.SearchArtistAsync("Pink Floyd");

        Assert.AreEqual(1, handler.ReceivedRequests.Count);
        var url = handler.ReceivedRequests[0].RequestUri!.ToString();
        Assert.IsTrue(url.Contains("artist/"), $"URL should contain 'artist/': {url}");
        Assert.IsTrue(url.Contains("Pink") && url.Contains("Floyd"), $"URL should contain artist name: {url}");
        Assert.IsTrue(url.Contains("fmt=json"), $"URL should contain 'fmt=json': {url}");
    }

    [TestMethod]
    public async Task SearchReleaseGroup_BuildsCorrectUrl()
    {
        var handler = MockHttpMessageHandler.ForJson("""{"release-groups":[]}""");
        var client = CreateClient(handler);

        await client.SearchReleaseGroupAsync("Dark Side of the Moon", "Pink Floyd");

        Assert.AreEqual(1, handler.ReceivedRequests.Count);
        var url = handler.ReceivedRequests[0].RequestUri!.ToString();
        Assert.IsTrue(url.Contains("release-group/"), $"URL should contain 'release-group/': {url}");
        Assert.IsTrue(url.Contains("fmt=json"), $"URL should contain 'fmt=json': {url}");
    }

    [TestMethod]
    public async Task SearchRecording_BuildsCorrectUrl()
    {
        var handler = MockHttpMessageHandler.ForJson("""{"recordings":[]}""");
        var client = CreateClient(handler);

        await client.SearchRecordingAsync("Money", "Pink Floyd");

        Assert.AreEqual(1, handler.ReceivedRequests.Count);
        var url = handler.ReceivedRequests[0].RequestUri!.ToString();
        Assert.IsTrue(url.Contains("recording/"), $"URL should contain 'recording/': {url}");
        Assert.IsTrue(url.Contains("fmt=json"), $"URL should contain 'fmt=json': {url}");
    }

    [TestMethod]
    public async Task GetArtist_IncludesRelations()
    {
        var json = """{"id":"83d91898-7763-47d7-b03b-faaee372db71","name":"Pink Floyd","relations":[],"annotation":null}""";
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        await client.GetArtistAsync("83d91898-7763-47d7-b03b-faaee372db71");

        var url = handler.ReceivedRequests[0].RequestUri!.ToString();
        Assert.IsTrue(url.Contains("inc=url-rels"), $"URL should contain 'inc=url-rels': {url}");
        Assert.IsTrue(url.Contains("annotation"), $"URL should contain 'annotation': {url}");
    }

    [TestMethod]
    public async Task GetReleaseGroup_IncludesReleases()
    {
        var json = """{"id":"test-id","title":"Test","releases":[]}""";
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        await client.GetReleaseGroupAsync("test-id");

        var url = handler.ReceivedRequests[0].RequestUri!.ToString();
        Assert.IsTrue(url.Contains("inc=releases"), $"URL should contain 'inc=releases': {url}");
    }

    [TestMethod]
    public async Task AllRequests_IncludeUserAgent()
    {
        var handler = MockHttpMessageHandler.ForJson("""{"artists":[]}""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://musicbrainz.org/ws/2/") };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetCloud/0.2.0 (https://github.com/LLabmik/DotNetCloud)");
        var client = new MusicBrainzClient(httpClient, _rateLimiter, NullLogger<MusicBrainzClient>.Instance);

        await client.SearchArtistAsync("Test");

        var userAgent = handler.ReceivedRequests[0].Headers.UserAgent.ToString();
        Assert.IsTrue(userAgent.Contains("DotNetCloud"), $"User-Agent should contain 'DotNetCloud': {userAgent}");
    }

    // ── JSON Deserialization ─────────────────────────────────────────

    [TestMethod]
    public async Task SearchArtist_DeserializesResults()
    {
        var json = """
        {
            "artists": [
                {"id":"83d91898-7763-47d7-b03b-faaee372db71","name":"Pink Floyd","score":100,"disambiguation":""},
                {"id":"artist2","name":"Pink Floyd (tribute)","score":75,"disambiguation":"tribute band"},
                {"id":"artist3","name":"Pink Floydian","score":50,"disambiguation":"solo project"}
            ]
        }
        """;
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var results = await client.SearchArtistAsync("Pink Floyd");

        Assert.IsNotNull(results);
        Assert.AreEqual(3, results.Count);
        Assert.AreEqual("83d91898-7763-47d7-b03b-faaee372db71", results[0].Id);
        Assert.AreEqual("Pink Floyd", results[0].Name);
        Assert.AreEqual(100, results[0].Score);
        Assert.AreEqual("tribute band", results[1].Disambiguation);
    }

    [TestMethod]
    public async Task SearchArtist_EmptyResults_ReturnsEmptyList()
    {
        var handler = MockHttpMessageHandler.ForJson("""{"artists":[]}""");
        var client = CreateClient(handler);

        var results = await client.SearchArtistAsync("QXZXQNOTANARTIST");

        Assert.IsNotNull(results);
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task GetArtist_DeserializesRelations()
    {
        var json = """
        {
            "id":"83d91898-7763-47d7-b03b-faaee372db71",
            "name":"Pink Floyd",
            "annotation":null,
            "relations":[
                {"type":"wikipedia","url":{"resource":"https://en.wikipedia.org/wiki/Pink_Floyd"}},
                {"type":"discogs","url":{"resource":"https://www.discogs.com/artist/45467-Pink-Floyd"}},
                {"type":"official homepage","url":{"resource":"https://www.pinkfloyd.com"}}
            ]
        }
        """;
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var detail = await client.GetArtistAsync("83d91898-7763-47d7-b03b-faaee372db71");

        Assert.IsNotNull(detail);
        Assert.AreEqual("https://en.wikipedia.org/wiki/Pink_Floyd", detail.WikipediaUrl);
        Assert.AreEqual("https://www.discogs.com/artist/45467-Pink-Floyd", detail.DiscogsUrl);
        Assert.AreEqual("https://www.pinkfloyd.com", detail.OfficialUrl);
    }

    [TestMethod]
    public async Task GetArtist_DeserializesAnnotation()
    {
        var json = """
        {
            "id":"83d91898-7763-47d7-b03b-faaee372db71",
            "name":"Pink Floyd",
            "annotation":"Pink Floyd were an English rock band formed in London in 1965.",
            "relations":[]
        }
        """;
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var detail = await client.GetArtistAsync("83d91898-7763-47d7-b03b-faaee372db71");

        Assert.IsNotNull(detail);
        Assert.AreEqual("Pink Floyd were an English rock band formed in London in 1965.", detail.Annotation);
    }

    [TestMethod]
    public async Task GetReleaseGroup_DeserializesReleases()
    {
        var json = """
        {
            "id":"rg-1",
            "title":"The Dark Side of the Moon",
            "releases":[
                {"id":"r1","title":"The Dark Side of the Moon","date":"1973-03-01","country":"US"},
                {"id":"r2","title":"The Dark Side of the Moon","date":"1973-03-01","country":"GB"},
                {"id":"r3","title":"The Dark Side of the Moon (Remaster)","date":"2003","country":"US"}
            ]
        }
        """;
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var detail = await client.GetReleaseGroupAsync("rg-1");

        Assert.IsNotNull(detail);
        Assert.AreEqual("The Dark Side of the Moon", detail.Title);
        Assert.AreEqual(3, detail.Releases.Count);
        Assert.AreEqual("r1", detail.Releases[0].Id);
        Assert.AreEqual("US", detail.Releases[0].Country);
    }

    [TestMethod]
    public async Task SearchArtist_MalformedJson_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForJson("{{not valid json at all!!");
        var client = CreateClient(handler);

        var results = await client.SearchArtistAsync("Pink Floyd");

        Assert.IsNull(results);
    }

    // ── Rate Limiting ────────────────────────────────────────────────

    [TestMethod]
    public async Task ConcurrentRequests_RespectRateLimit()
    {
        // Use a real rate limiter with 200ms delay for testable concurrency
        using var limiter = new MusicBrainzRateLimiter(200);
        var handler = MockHttpMessageHandler.ForJson("""{"artists":[]}""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://musicbrainz.org/ws/2/") };
        var client = new MusicBrainzClient(httpClient, limiter, NullLogger<MusicBrainzClient>.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, 3).Select(_ => client.SearchArtistAsync("Test")).ToArray();
        await Task.WhenAll(tasks);
        sw.Stop();

        // 3 requests with 200ms gap should take at least 400ms
        Assert.IsTrue(sw.ElapsedMilliseconds >= 350, $"Expected at least 350ms, got {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task SequentialRequests_DelayBetween()
    {
        using var limiter = new MusicBrainzRateLimiter(150);
        var handler = MockHttpMessageHandler.ForJson("""{"artists":[]}""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://musicbrainz.org/ws/2/") };
        var client = new MusicBrainzClient(httpClient, limiter, NullLogger<MusicBrainzClient>.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await client.SearchArtistAsync("Test1");
        await client.SearchArtistAsync("Test2");
        sw.Stop();

        Assert.IsTrue(sw.ElapsedMilliseconds >= 100, $"Expected at least 100ms, got {sw.ElapsedMilliseconds}ms");
    }

    // ── Error Handling ───────────────────────────────────────────────

    [TestMethod]
    public async Task SearchArtist_Http503_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForStatus(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        var results = await client.SearchArtistAsync("Test");

        Assert.IsNull(results);
    }

    [TestMethod]
    public async Task SearchArtist_Http429_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForStatus(HttpStatusCode.TooManyRequests);
        var client = CreateClient(handler);

        var results = await client.SearchArtistAsync("Test");

        Assert.IsNull(results);
    }

    [TestMethod]
    public async Task GetArtist_NetworkError_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForNetworkError();
        var client = CreateClient(handler);

        var result = await client.GetArtistAsync("83d91898-7763-47d7-b03b-faaee372db71");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetArtist_Timeout_ReturnsNull()
    {
        var handler = MockHttpMessageHandler.ForTimeout();
        var client = CreateClient(handler);

        var result = await client.GetArtistAsync("83d91898-7763-47d7-b03b-faaee372db71");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SearchRecording_DeserializesResults()
    {
        var json = """
        {
            "recordings": [
                {"id":"rec-1","title":"Money","score":95,"length":382000},
                {"id":"rec-2","title":"Money (Live)","score":70,"length":450000}
            ]
        }
        """;
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var results = await client.SearchRecordingAsync("Money", "Pink Floyd");

        Assert.IsNotNull(results);
        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("rec-1", results[0].Id);
        Assert.AreEqual("Money", results[0].Title);
        Assert.AreEqual(95, results[0].Score);
        Assert.AreEqual(382000, results[0].Length);
    }

    [TestMethod]
    public async Task GetRecording_DeserializesResult()
    {
        var json = """{"id":"rec-1","title":"Money","length":382000}""";
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var result = await client.GetRecordingAsync("rec-1");

        Assert.IsNotNull(result);
        Assert.AreEqual("rec-1", result.Id);
        Assert.AreEqual("Money", result.Title);
        Assert.AreEqual(382000, result.Length);
    }

    [TestMethod]
    public async Task SearchReleaseGroup_DeserializesResults()
    {
        var json = """
        {
            "release-groups": [
                {"id":"rg-1","title":"The Dark Side of the Moon","score":100,"primary-type":"Album"},
                {"id":"rg-2","title":"Dark Side of the Moon (Live)","score":60,"primary-type":"Album"}
            ]
        }
        """;
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var results = await client.SearchReleaseGroupAsync("The Dark Side of the Moon", "Pink Floyd");

        Assert.IsNotNull(results);
        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("rg-1", results[0].Id);
        Assert.AreEqual("The Dark Side of the Moon", results[0].Title);
        Assert.AreEqual(100, results[0].Score);
        Assert.AreEqual("Album", results[0].PrimaryType);
    }

    [TestMethod]
    public async Task GetArtist_NoRelations_UrlsAreNull()
    {
        var json = """{"id":"a1","name":"Test","relations":[],"annotation":null}""";
        var handler = MockHttpMessageHandler.ForJson(json);
        var client = CreateClient(handler);

        var detail = await client.GetArtistAsync("a1");

        Assert.IsNotNull(detail);
        Assert.IsNull(detail.WikipediaUrl);
        Assert.IsNull(detail.DiscogsUrl);
        Assert.IsNull(detail.OfficialUrl);
        Assert.IsNull(detail.Annotation);
    }

    [TestMethod]
    public async Task SearchArtist_NullArtistsField_ReturnsEmptyList()
    {
        var handler = MockHttpMessageHandler.ForJson("{}");
        var client = CreateClient(handler);

        var results = await client.SearchArtistAsync("Test");

        Assert.IsNotNull(results);
        Assert.AreEqual(0, results.Count);
    }
}
