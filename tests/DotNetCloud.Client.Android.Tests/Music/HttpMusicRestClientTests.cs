using System.Net;
using System.Text.Json;
using DotNetCloud.Client.Android.Music;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace DotNetCloud.Client.Android.Tests.Music;

[TestClass]
public sealed class HttpMusicRestClientTests
{
    private const string ServerUrl = "https://example.com:15443";
    private const string AccessToken = "test-access-token-123";

    private Mock<HttpMessageHandler> _handler = null!;
    private HttpClient _httpClient = null!;
    private HttpMusicRestClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_handler.Object);
        _client = new HttpMusicRestClient(_httpClient, new Mock<ILogger<HttpMusicRestClient>>().Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { _httpClient.Dispose(); } catch { }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static string GetUrl(HttpRequestMessage m) =>
        m.RequestUri?.ToString() ?? string.Empty;

    private void SetupGetResponse(string urlPattern, HttpStatusCode statusCode, object? responseData)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["data"] = responseData
        };
        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && GetUrl(m).Contains(urlPattern)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(json)
            });
    }

    private void SetupPostResponse(string urlPattern, HttpStatusCode statusCode, string json)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    GetUrl(m).Contains(urlPattern)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(json)
            });
    }

    private void SetupNotFound(string urlPattern)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && GetUrl(m).Contains(urlPattern)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("{\"success\":false,\"data\":null}")
            });
    }

    // ── Artists ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListArtistsAsync_ReturnsArtists()
    {
        var artists = new List<ArtistDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Artist One", CreatedAt = DateTime.UtcNow, AlbumCount = 3, TrackCount = 15 },
            new() { Id = Guid.NewGuid(), Name = "Artist Two", CreatedAt = DateTime.UtcNow, AlbumCount = 1, TrackCount = 8 }
        };
        SetupGetResponse("/api/v1/music/artists?skip=0&take=50", HttpStatusCode.OK, artists);

        var result = await _client.ListArtistsAsync(ServerUrl, AccessToken);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Artist One", result[0].Name);
        Assert.AreEqual("Artist Two", result[1].Name);
    }

    [TestMethod]
    public async Task ListArtistsAsync_ReturnsEmpty_WhenNullData()
    {
        SetupGetResponse("/api/v1/music/artists?skip=0&take=50", HttpStatusCode.OK, null);

        var result = await _client.ListArtistsAsync(ServerUrl, AccessToken);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetArtistAsync_ReturnsArtist()
    {
        var artistId = Guid.NewGuid();
        var artist = new ArtistDto { Id = artistId, Name = "Test Artist", CreatedAt = DateTime.UtcNow };
        SetupGetResponse($"/api/v1/music/artists/{artistId}", HttpStatusCode.OK, artist);

        var result = await _client.GetArtistAsync(ServerUrl, AccessToken, artistId);

        Assert.IsNotNull(result);
        Assert.AreEqual(artistId, result.Id);
        Assert.AreEqual("Test Artist", result.Name);
    }

    [TestMethod]
    public async Task GetArtistAsync_ReturnsNull_On404()
    {
        var artistId = Guid.NewGuid();
        SetupNotFound($"/api/v1/music/artists/{artistId}");

        var result = await _client.GetArtistAsync(ServerUrl, AccessToken, artistId);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SearchArtistsAsync_ReturnsMatchingArtists()
    {
        var artists = new List<ArtistDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Search Result", CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse("/api/v1/music/artists/search?q=Search&take=20", HttpStatusCode.OK, artists);

        var result = await _client.SearchArtistsAsync(ServerUrl, AccessToken, "Search");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Search Result", result[0].Name);
    }

    // ── Albums ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListAlbumsAsync_ReturnsAlbums()
    {
        var artistId = Guid.NewGuid();
        var albums = new List<MusicAlbumDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Album One", ArtistId = artistId, ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse("/api/v1/music/albums?skip=0&take=50", HttpStatusCode.OK, albums);

        var result = await _client.ListAlbumsAsync(ServerUrl, AccessToken);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Album One", result[0].Title);
    }

    [TestMethod]
    public async Task GetAlbumAsync_ReturnsAlbum()
    {
        var albumId = Guid.NewGuid();
        var album = new MusicAlbumDto { Id = albumId, Title = "Test Album", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        SetupGetResponse($"/api/v1/music/albums/{albumId}", HttpStatusCode.OK, album);

        var result = await _client.GetAlbumAsync(ServerUrl, AccessToken, albumId);

        Assert.IsNotNull(result);
        Assert.AreEqual(albumId, result.Id);
    }

    [TestMethod]
    public async Task GetAlbumAsync_ReturnsNull_On404()
    {
        var albumId = Guid.NewGuid();
        SetupNotFound($"/api/v1/music/albums/{albumId}");

        var result = await _client.GetAlbumAsync(ServerUrl, AccessToken, albumId);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListAlbumsByArtistAsync_ReturnsAlbums()
    {
        var artistId = Guid.NewGuid();
        var albums = new List<MusicAlbumDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Artist Album", ArtistId = artistId, ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse($"/api/v1/music/artists/{artistId}/albums", HttpStatusCode.OK, albums);

        var result = await _client.ListAlbumsByArtistAsync(ServerUrl, AccessToken, artistId);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task SearchAlbumsAsync_ReturnsAlbums()
    {
        var albums = new List<MusicAlbumDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Search Album", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse("/api/v1/music/albums/search?q=Search&take=20", HttpStatusCode.OK, albums);

        var result = await _client.SearchAlbumsAsync(ServerUrl, AccessToken, "Search");

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task GetRecentAlbumsAsync_ReturnsRecent()
    {
        var albums = new List<MusicAlbumDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Recent Album", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse("/api/v1/music/albums/recent?take=20", HttpStatusCode.OK, albums);

        var result = await _client.GetRecentAlbumsAsync(ServerUrl, AccessToken);

        Assert.AreEqual(1, result.Count);
    }

    // ── Tracks ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListTracksAsync_ReturnsTracks()
    {
        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Track One", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse("/api/v1/music/tracks?skip=0&take=50", HttpStatusCode.OK, tracks);

        var result = await _client.ListTracksAsync(ServerUrl, AccessToken);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Track One", result[0].Title);
    }

    [TestMethod]
    public async Task GetTrackAsync_ReturnsTrack()
    {
        var trackId = Guid.NewGuid();
        var track = new TrackDto { Id = trackId, Title = "Test Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/flac", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow };
        SetupGetResponse($"/api/v1/music/tracks/{trackId}", HttpStatusCode.OK, track);

        var result = await _client.GetTrackAsync(ServerUrl, AccessToken, trackId);

        Assert.IsNotNull(result);
        Assert.AreEqual(trackId, result.Id);
    }

    [TestMethod]
    public async Task GetTrackAsync_ReturnsNull_On404()
    {
        var trackId = Guid.NewGuid();
        SetupNotFound($"/api/v1/music/tracks/{trackId}");

        var result = await _client.GetTrackAsync(ServerUrl, AccessToken, trackId);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListTracksByAlbumAsync_ReturnsTracks()
    {
        var albumId = Guid.NewGuid();
        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Album Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/ogg", AlbumId = albumId, ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse($"/api/v1/music/albums/{albumId}/tracks", HttpStatusCode.OK, tracks);

        var result = await _client.ListTracksByAlbumAsync(ServerUrl, AccessToken, albumId);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task SearchTracksAsync_ReturnsTracks()
    {
        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Search Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse("/api/v1/music/tracks/search?q=Search&take=20", HttpStatusCode.OK, tracks);

        var result = await _client.SearchTracksAsync(ServerUrl, AccessToken, "Search");

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task GetRandomTracksAsync_ReturnsTracks()
    {
        SetupGetResponse("/api/v1/music/tracks/random?take=10", HttpStatusCode.OK, new List<TrackDto>());

        var result = await _client.GetRandomTracksAsync(ServerUrl, AccessToken, take: 10);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetRandomTracksAsync_WithGenre_IncludesGenreParam()
    {
        SetupGetResponse("/api/v1/music/tracks/random?take=20&genre=Rock", HttpStatusCode.OK, new List<TrackDto>());

        var result = await _client.GetRandomTracksAsync(ServerUrl, AccessToken, genre: "Rock");

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetRecentTracksAsync_ReturnsRecent()
    {
        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Recent Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/mpeg", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse("/api/v1/music/tracks/recent?take=20", HttpStatusCode.OK, tracks);

        var result = await _client.GetRecentTracksAsync(ServerUrl, AccessToken);

        Assert.AreEqual(1, result.Count);
    }

    // ── Playlists ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListPlaylistsAsync_ReturnsPlaylists()
    {
        var playlists = new List<PlaylistDto>
        {
            new() { Id = Guid.NewGuid(), Name = "My Playlist", OwnerId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse("/api/v1/music/playlists", HttpStatusCode.OK, playlists);

        var result = await _client.ListPlaylistsAsync(ServerUrl, AccessToken);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("My Playlist", result[0].Name);
    }

    [TestMethod]
    public async Task GetPlaylistAsync_ReturnsPlaylist()
    {
        var playlistId = Guid.NewGuid();
        var playlist = new PlaylistDto { Id = playlistId, Name = "Test Playlist", OwnerId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        SetupGetResponse($"/api/v1/music/playlists/{playlistId}", HttpStatusCode.OK, playlist);

        var result = await _client.GetPlaylistAsync(ServerUrl, AccessToken, playlistId);

        Assert.IsNotNull(result);
        Assert.AreEqual(playlistId, result.Id);
    }

    [TestMethod]
    public async Task GetPlaylistAsync_ReturnsNull_On404()
    {
        var playlistId = Guid.NewGuid();
        SetupNotFound($"/api/v1/music/playlists/{playlistId}");

        var result = await _client.GetPlaylistAsync(ServerUrl, AccessToken, playlistId);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetPlaylistTracksAsync_ReturnsTracks()
    {
        var playlistId = Guid.NewGuid();
        var tracks = new List<TrackDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Playlist Track", OwnerId = Guid.NewGuid(), FileNodeId = Guid.NewGuid(), MimeType = "audio/flac", ArtistId = Guid.NewGuid(), ArtistName = "Artist", CreatedAt = DateTime.UtcNow }
        };
        SetupGetResponse($"/api/v1/music/playlists/{playlistId}/tracks", HttpStatusCode.OK, tracks);

        var result = await _client.GetPlaylistTracksAsync(ServerUrl, AccessToken, playlistId);

        Assert.AreEqual(1, result.Count);
    }

    // ── Playback / Stars ───────────────────────────────────────────────

    [TestMethod]
    public async Task RecordPlayAsync_Succeeds()
    {
        SetupPostResponse("/api/v1/music/tracks", HttpStatusCode.OK, "{\"success\":true,\"data\":{\"recorded\":true}}");

        await _client.RecordPlayAsync(ServerUrl, AccessToken, Guid.NewGuid());
    }

    [TestMethod]
    public async Task ToggleStarAsync_Track_StarsTrack()
    {
        SetupPostResponse("/api/v1/music/tracks/", HttpStatusCode.OK, "{\"success\":true,\"data\":{\"toggled\":true}}");

        await _client.ToggleStarAsync(ServerUrl, AccessToken, Guid.NewGuid(), "track");
    }

    [TestMethod]
    public async Task ToggleStarAsync_InvalidType_ThrowsArgumentException()
    {
        try
        {
            await _client.ToggleStarAsync(ServerUrl, AccessToken, Guid.NewGuid(), "invalid");
            Assert.Fail("Expected ArgumentException was not thrown.");
        }
        catch (ArgumentException ex)
        {
            StringAssert.Contains(ex.Message, "unknown itemType", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Equalizer ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListEqPresetsAsync_ReturnsPresets()
    {
        var presets = new List<EqPresetDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Rock", IsBuiltIn = true, Bands = new Dictionary<string, double> { ["62"] = 0 } }
        };
        SetupGetResponse("/api/v1/music/eq/presets", HttpStatusCode.OK, presets);

        var result = await _client.ListEqPresetsAsync(ServerUrl, AccessToken);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Rock", result[0].Name);
    }

    [TestMethod]
    public async Task GetEqPresetAsync_ReturnsPreset()
    {
        var presetId = Guid.NewGuid();
        SetupGetResponse($"/api/v1/music/eq/presets/{presetId}", HttpStatusCode.OK,
            new EqPresetDto { Id = presetId, Name = "Jazz", IsBuiltIn = true, Bands = new Dictionary<string, double>() });

        var result = await _client.GetEqPresetAsync(ServerUrl, AccessToken, presetId);

        Assert.IsNotNull(result);
        Assert.AreEqual(presetId, result.Id);
    }

    [TestMethod]
    public async Task GetEqPresetAsync_ReturnsNull_On404()
    {
        var presetId = Guid.NewGuid();
        SetupNotFound($"/api/v1/music/eq/presets/{presetId}");

        var result = await _client.GetEqPresetAsync(ServerUrl, AccessToken, presetId);

        Assert.IsNull(result);
    }

    // ── Genres ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetGenresAsync_ReturnsGenres()
    {
        var genres = new List<string> { "Rock", "Jazz", "Classical" };
        SetupGetResponse("/api/v1/music/genres", HttpStatusCode.OK, genres);

        var result = await _client.GetGenresAsync(ServerUrl, AccessToken);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("Rock", result[0]);
    }

    [TestMethod]
    public async Task GetGenresAsync_ReturnsEmpty_WhenNull()
    {
        SetupGetResponse("/api/v1/music/genres", HttpStatusCode.OK, null);

        var result = await _client.GetGenresAsync(ServerUrl, AccessToken);

        Assert.AreEqual(0, result.Count);
    }

    // ── Auth header verification ───────────────────────────────────────

    [TestMethod]
    public async Task Request_HasBearerToken()
    {
        HttpRequestMessage? captured = null;
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\":true,\"data\":[]}")
            })
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req);

        await _client.ListArtistsAsync(ServerUrl, AccessToken);

        Assert.IsNotNull(captured);
        Assert.IsNotNull(captured.Headers.Authorization);
        Assert.AreEqual("Bearer", captured.Headers.Authorization.Scheme);
        Assert.AreEqual(AccessToken, captured.Headers.Authorization.Parameter);
    }
}
