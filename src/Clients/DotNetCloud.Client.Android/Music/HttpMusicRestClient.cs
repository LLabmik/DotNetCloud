using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Music;

/// <summary>
/// <see cref="IMusicRestClient"/> implementation backed by <see cref="HttpClient"/>.
/// Registered via <c>AddHttpClient&lt;IMusicRestClient, HttpMusicRestClient&gt;()</c>.
/// Uses the same auth pattern as <see cref="Files.HttpFileRestClient"/> and
/// <see cref="Chat.HttpChatRestClient"/> — sets the Bearer token on DefaultRequestHeaders.
/// </summary>
internal sealed class HttpMusicRestClient : IMusicRestClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly ILogger<HttpMusicRestClient> _logger;

    /// <summary>Initializes a new <see cref="HttpMusicRestClient"/>.</summary>
    public HttpMusicRestClient(HttpClient http, ILogger<HttpMusicRestClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    private void SetAuth(string accessToken) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

    private static string BaseUrl(string serverBaseUrl) => serverBaseUrl.TrimEnd('/');

    private async Task<T?> GetEnvelopeDataAsync<T>(string url, string accessToken, CancellationToken ct)
    {
        SetAuth(accessToken);
#if ANDROID
        global::Android.Util.Log.Info("DotNetCloud", $"MUSIC: GET {url}");
#endif
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
#if ANDROID
            global::Android.Util.Log.Error("DotNetCloud", $"MUSIC: {(int)response.StatusCode} for {url}: {body}");
#endif
        }
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<T>(response, ct).ConfigureAwait(false);
    }

    private static async Task<T?> ReadEnvelopeDataAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("data", out var dataProp))
        {
            return dataProp.Deserialize<T>(JsonOpts);
        }

        return doc.RootElement.Deserialize<T>(JsonOpts);
    }

    private async Task PostAsync(string url, string accessToken, CancellationToken ct)
    {
        SetAuth(accessToken);
        using var response = await _http.PostAsync(url, null, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    // ── Artists ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ArtistDto>> ListArtistsAsync(
        string serverBaseUrl, string accessToken,
        int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/artists?skip={skip}&take={take}";
        var data = await GetEnvelopeDataAsync<List<ArtistDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<ArtistDto?> GetArtistAsync(
        string serverBaseUrl, string accessToken,
        Guid artistId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/artists/{artistId}";
        try
        {
            return await GetEnvelopeDataAsync<ArtistDto>(url, accessToken, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ArtistDto>> SearchArtistsAsync(
        string serverBaseUrl, string accessToken,
        string query, int take = 20, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/artists/search?q={Uri.EscapeDataString(query)}&take={take}";
        var data = await GetEnvelopeDataAsync<List<ArtistDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetArtistAlphabetAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/artists/alphabet";
        var data = await GetEnvelopeDataAsync<List<string>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    // ── Albums ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsAsync(
        string serverBaseUrl, string accessToken,
        int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/albums?skip={skip}&take={take}";
        var data = await GetEnvelopeDataAsync<List<MusicAlbumDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<MusicAlbumDto?> GetAlbumAsync(
        string serverBaseUrl, string accessToken,
        Guid albumId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/albums/{albumId}";
        try
        {
            return await GetEnvelopeDataAsync<MusicAlbumDto>(url, accessToken, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsByArtistAsync(
        string serverBaseUrl, string accessToken,
        Guid artistId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/artists/{artistId}/albums";
        var data = await GetEnvelopeDataAsync<List<MusicAlbumDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MusicAlbumDto>> SearchAlbumsAsync(
        string serverBaseUrl, string accessToken,
        string query, int take = 20, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/albums/search?q={Uri.EscapeDataString(query)}&take={take}";
        var data = await GetEnvelopeDataAsync<List<MusicAlbumDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAlbumAlphabetAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/albums/alphabet";
        var data = await GetEnvelopeDataAsync<List<string>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MusicAlbumDto>> GetRecentAlbumsAsync(
        string serverBaseUrl, string accessToken,
        int take = 20, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/albums/recent?take={take}";
        var data = await GetEnvelopeDataAsync<List<MusicAlbumDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    // ── Tracks ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackDto>> ListTracksAsync(
        string serverBaseUrl, string accessToken,
        int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/tracks?skip={skip}&take={take}";
        var data = await GetEnvelopeDataAsync<List<TrackDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<TrackDto?> GetTrackAsync(
        string serverBaseUrl, string accessToken,
        Guid trackId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/tracks/{trackId}";
        try
        {
            return await GetEnvelopeDataAsync<TrackDto>(url, accessToken, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackDto>> ListTracksByAlbumAsync(
        string serverBaseUrl, string accessToken,
        Guid albumId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/albums/{albumId}/tracks";
        var data = await GetEnvelopeDataAsync<List<TrackDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackDto>> SearchTracksAsync(
        string serverBaseUrl, string accessToken,
        string query, int take = 20, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/tracks/search?q={Uri.EscapeDataString(query)}&take={take}";
        var data = await GetEnvelopeDataAsync<List<TrackDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetTrackAlphabetAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/tracks/alphabet";
        var data = await GetEnvelopeDataAsync<List<string>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackDto>> GetRandomTracksAsync(
        string serverBaseUrl, string accessToken,
        int take = 20, string? genre = null, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/tracks/random?take={take}";
        if (!string.IsNullOrEmpty(genre))
            url += $"&genre={Uri.EscapeDataString(genre)}";
        var data = await GetEnvelopeDataAsync<List<TrackDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackDto>> GetRecentTracksAsync(
        string serverBaseUrl, string accessToken,
        int take = 20, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/tracks/recent?take={take}";
        var data = await GetEnvelopeDataAsync<List<TrackDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    // ── Playlists ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlaylistDto>> ListPlaylistsAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/playlists";
        var data = await GetEnvelopeDataAsync<List<PlaylistDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<PlaylistDto?> GetPlaylistAsync(
        string serverBaseUrl, string accessToken,
        Guid playlistId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/playlists/{playlistId}";
        try
        {
            return await GetEnvelopeDataAsync<PlaylistDto>(url, accessToken, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackDto>> GetPlaylistTracksAsync(
        string serverBaseUrl, string accessToken,
        Guid playlistId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/playlists/{playlistId}/tracks";
        var data = await GetEnvelopeDataAsync<List<TrackDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    // ── Playback / Stars ─────────────────────────────────────────────

    /// <inheritdoc />
    public async Task RecordPlayAsync(
        string serverBaseUrl, string accessToken,
        Guid trackId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/tracks/{trackId}/play";
        await PostAsync(url, accessToken, ct);
    }

    /// <inheritdoc />
    public async Task ToggleStarAsync(
        string serverBaseUrl, string accessToken,
        Guid itemId, string itemType, CancellationToken ct = default)
    {
        var typePlural = itemType.ToLowerInvariant() switch
        {
            "track" => "tracks",
            "album" => "albums",
            "artist" => "artists",
            _ => throw new ArgumentException($"Unknown itemType: {itemType}")
        };
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/{typePlural}/{itemId}/star";
        await PostAsync(url, accessToken, ct);
    }

    // ── Equalizer ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<EqPresetDto>> ListEqPresetsAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/eq/presets";
        var data = await GetEnvelopeDataAsync<List<EqPresetDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<EqPresetDto?> GetEqPresetAsync(
        string serverBaseUrl, string accessToken,
        Guid presetId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/eq/presets/{presetId}";
        try
        {
            return await GetEnvelopeDataAsync<EqPresetDto>(url, accessToken, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // ── Genres ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetGenresAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/music/genres";
        var data = await GetEnvelopeDataAsync<List<string>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }
}
