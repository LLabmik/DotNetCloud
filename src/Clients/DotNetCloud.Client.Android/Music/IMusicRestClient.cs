using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Music;

/// <summary>
/// REST API client for music operations against a DotNetCloud server.
/// Follows the same per-call credentials pattern as <see cref="Files.IFileRestClient"/>.
/// </summary>
public interface IMusicRestClient
{
    // ── Artists ──────────────────────────────────────────────────────

    /// <summary>Lists artists with pagination.</summary>
    Task<IReadOnlyList<ArtistDto>> ListArtistsAsync(
        string serverBaseUrl, string accessToken,
        int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>Gets a single artist by ID.</summary>
    Task<ArtistDto?> GetArtistAsync(
        string serverBaseUrl, string accessToken,
        Guid artistId, CancellationToken ct = default);

    /// <summary>Searches artists by name.</summary>
    Task<IReadOnlyList<ArtistDto>> SearchArtistsAsync(
        string serverBaseUrl, string accessToken,
        string query, int take = 20, CancellationToken ct = default);

    /// <summary>Gets the distinct first characters of all artist names (server-side alphabet).</summary>
    Task<IReadOnlyList<string>> GetArtistAlphabetAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);

    // ── Albums ───────────────────────────────────────────────────────

    /// <summary>Lists all albums with pagination.</summary>
    Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsAsync(
        string serverBaseUrl, string accessToken,
        int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>Gets a single album by ID.</summary>
    Task<MusicAlbumDto?> GetAlbumAsync(
        string serverBaseUrl, string accessToken,
        Guid albumId, CancellationToken ct = default);

    /// <summary>Lists albums by a specific artist.</summary>
    Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsByArtistAsync(
        string serverBaseUrl, string accessToken,
        Guid artistId, CancellationToken ct = default);

    /// <summary>Searches albums by title.</summary>
    Task<IReadOnlyList<MusicAlbumDto>> SearchAlbumsAsync(
        string serverBaseUrl, string accessToken,
        string query, int take = 20, CancellationToken ct = default);

    /// <summary>Gets recently added albums.</summary>
    Task<IReadOnlyList<MusicAlbumDto>> GetRecentAlbumsAsync(
        string serverBaseUrl, string accessToken,
        int take = 20, CancellationToken ct = default);

    /// <summary>Gets the distinct first characters of all album titles (server-side alphabet).</summary>
    Task<IReadOnlyList<string>> GetAlbumAlphabetAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);

    // ── Tracks ───────────────────────────────────────────────────────

    /// <summary>Lists all tracks with pagination.</summary>
    Task<IReadOnlyList<TrackDto>> ListTracksAsync(
        string serverBaseUrl, string accessToken,
        int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>Gets a single track by ID.</summary>
    Task<TrackDto?> GetTrackAsync(
        string serverBaseUrl, string accessToken,
        Guid trackId, CancellationToken ct = default);

    /// <summary>Lists tracks for a specific album.</summary>
    Task<IReadOnlyList<TrackDto>> ListTracksByAlbumAsync(
        string serverBaseUrl, string accessToken,
        Guid albumId, CancellationToken ct = default);

    /// <summary>Searches tracks by title.</summary>
    Task<IReadOnlyList<TrackDto>> SearchTracksAsync(
        string serverBaseUrl, string accessToken,
        string query, int take = 20, CancellationToken ct = default);

    /// <summary>Gets random tracks, optionally filtered by genre.</summary>
    Task<IReadOnlyList<TrackDto>> GetRandomTracksAsync(
        string serverBaseUrl, string accessToken,
        int take = 20, string? genre = null, CancellationToken ct = default);

    /// <summary>Gets recently added tracks.</summary>
    Task<IReadOnlyList<TrackDto>> GetRecentTracksAsync(
        string serverBaseUrl, string accessToken,
        int take = 20, CancellationToken ct = default);

    /// <summary>Gets the distinct first characters of all track titles (server-side alphabet).</summary>
    Task<IReadOnlyList<string>> GetTrackAlphabetAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);

    // ── Playlists ────────────────────────────────────────────────────

    /// <summary>Lists all playlists.</summary>
    Task<IReadOnlyList<PlaylistDto>> ListPlaylistsAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);

    /// <summary>Gets a single playlist by ID.</summary>
    Task<PlaylistDto?> GetPlaylistAsync(
        string serverBaseUrl, string accessToken,
        Guid playlistId, CancellationToken ct = default);

    /// <summary>Gets tracks in a playlist.</summary>
    Task<IReadOnlyList<TrackDto>> GetPlaylistTracksAsync(
        string serverBaseUrl, string accessToken,
        Guid playlistId, CancellationToken ct = default);

    // ── Playback / Stars ─────────────────────────────────────────────

    /// <summary>Records a play event for the given track.</summary>
    Task RecordPlayAsync(
        string serverBaseUrl, string accessToken,
        Guid trackId, CancellationToken ct = default);

    /// <summary>Toggles the star (favourite) state of a music item.</summary>
    Task ToggleStarAsync(
        string serverBaseUrl, string accessToken,
        Guid itemId, string itemType,
        CancellationToken ct = default);

    // ── Equalizer ────────────────────────────────────────────────────

    /// <summary>Lists all equalizer presets.</summary>
    Task<IReadOnlyList<EqPresetDto>> ListEqPresetsAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);

    /// <summary>Gets a single equalizer preset by ID.</summary>
    Task<EqPresetDto?> GetEqPresetAsync(
        string serverBaseUrl, string accessToken,
        Guid presetId, CancellationToken ct = default);

    /// <summary>Creates a new EQ preset from the current band settings.</summary>
    Task<EqPresetDto> CreateEqPresetAsync(
        string serverBaseUrl, string accessToken,
        SaveEqPresetDto dto, CancellationToken ct = default);

    /// <summary>Updates an existing EQ preset.</summary>
    Task<EqPresetDto> UpdateEqPresetAsync(
        string serverBaseUrl, string accessToken,
        Guid presetId, SaveEqPresetDto dto, CancellationToken ct = default);

    // ── Genres ───────────────────────────────────────────────────────

    /// <summary>Gets all available genres.</summary>
    Task<IReadOnlyList<string>> GetGenresAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);
}
