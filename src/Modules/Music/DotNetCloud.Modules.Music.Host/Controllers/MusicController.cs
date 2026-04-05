using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Music.Host.Controllers;

/// <summary>
/// REST API controller for music library management.
/// </summary>
[Route("api/v1/music")]
public class MusicController : MusicControllerBase
{
    private readonly ArtistService _artistService;
    private readonly MusicAlbumService _albumService;
    private readonly TrackService _trackService;
    private readonly PlaylistService _playlistService;
    private readonly PlaybackService _playbackService;
    private readonly RecommendationService _recommendationService;
    private readonly MusicStreamingService _streamingService;
    private readonly EqPresetService _eqPresetService;
    private readonly ILogger<MusicController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicController"/> class.
    /// </summary>
    public MusicController(
        ArtistService artistService,
        MusicAlbumService albumService,
        TrackService trackService,
        PlaylistService playlistService,
        PlaybackService playbackService,
        RecommendationService recommendationService,
        MusicStreamingService streamingService,
        EqPresetService eqPresetService,
        ILogger<MusicController> logger)
    {
        _artistService = artistService;
        _albumService = albumService;
        _trackService = trackService;
        _playlistService = playlistService;
        _playbackService = playbackService;
        _recommendationService = recommendationService;
        _streamingService = streamingService;
        _eqPresetService = eqPresetService;
        _logger = logger;
    }

    // ─── Artists ───────────────────────────────────────────────────────

    /// <summary>Lists artists in the library.</summary>
    [HttpGet("artists")]
    public async Task<IActionResult> ListArtists([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var artists = await _artistService.ListArtistsAsync(caller, skip, take);
        return Ok(Envelope(artists));
    }

    /// <summary>Gets an artist by ID.</summary>
    [HttpGet("artists/{artistId:guid}")]
    public async Task<IActionResult> GetArtist(Guid artistId)
    {
        var caller = GetAuthenticatedCaller();
        var artist = await _artistService.GetArtistAsync(artistId, caller);
        return artist is null
            ? NotFound(ErrorEnvelope(ErrorCodes.ArtistNotFound, "Artist not found."))
            : Ok(Envelope(artist));
    }

    /// <summary>Searches artists by name.</summary>
    [HttpGet("artists/search")]
    public async Task<IActionResult> SearchArtists([FromQuery] string q, [FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var artists = await _artistService.SearchAsync(caller, q, take);
        return Ok(Envelope(artists));
    }

    /// <summary>Deletes an artist (soft delete).</summary>
    [HttpDelete("artists/{artistId:guid}")]
    public async Task<IActionResult> DeleteArtist(Guid artistId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _artistService.DeleteArtistAsync(artistId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.ArtistNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.ArtistNotFound, ex.Message));
        }
    }

    // ─── Albums ───────────────────────────────────────────────────────

    /// <summary>Lists albums in the library.</summary>
    [HttpGet("albums")]
    public async Task<IActionResult> ListAlbums([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var albums = await _albumService.ListAlbumsAsync(caller, skip, take);
        return Ok(Envelope(albums));
    }

    /// <summary>Gets an album by ID.</summary>
    [HttpGet("albums/{albumId:guid}")]
    public async Task<IActionResult> GetAlbum(Guid albumId)
    {
        var caller = GetAuthenticatedCaller();
        var album = await _albumService.GetAlbumAsync(albumId, caller);
        return album is null
            ? NotFound(ErrorEnvelope(ErrorCodes.MusicAlbumNotFound, "Album not found."))
            : Ok(Envelope(album));
    }

    /// <summary>Lists albums by a specific artist.</summary>
    [HttpGet("artists/{artistId:guid}/albums")]
    public async Task<IActionResult> ListAlbumsByArtist(Guid artistId)
    {
        var caller = GetAuthenticatedCaller();
        var albums = await _albumService.ListAlbumsByArtistAsync(artistId, caller);
        return Ok(Envelope(albums));
    }

    /// <summary>Searches albums by title.</summary>
    [HttpGet("albums/search")]
    public async Task<IActionResult> SearchAlbums([FromQuery] string q, [FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var albums = await _albumService.SearchAsync(caller, q, take);
        return Ok(Envelope(albums));
    }

    /// <summary>Gets recently added albums.</summary>
    [HttpGet("albums/recent")]
    public async Task<IActionResult> GetRecentAlbums([FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var albums = await _albumService.GetRecentAlbumsAsync(caller, take);
        return Ok(Envelope(albums));
    }

    /// <summary>Deletes an album (soft delete).</summary>
    [HttpDelete("albums/{albumId:guid}")]
    public async Task<IActionResult> DeleteAlbum(Guid albumId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _albumService.DeleteAlbumAsync(albumId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.MusicAlbumNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.MusicAlbumNotFound, ex.Message));
        }
    }

    // ─── Tracks ───────────────────────────────────────────────────────

    /// <summary>Lists tracks in the library.</summary>
    [HttpGet("tracks")]
    public async Task<IActionResult> ListTracks([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var tracks = await _trackService.ListTracksAsync(caller, skip, take);
        return Ok(Envelope(tracks));
    }

    /// <summary>Gets a track by ID.</summary>
    [HttpGet("tracks/{trackId:guid}")]
    public async Task<IActionResult> GetTrack(Guid trackId)
    {
        var caller = GetAuthenticatedCaller();
        var track = await _trackService.GetTrackAsync(trackId, caller);
        return track is null
            ? NotFound(ErrorEnvelope(ErrorCodes.TrackNotFound, "Track not found."))
            : Ok(Envelope(track));
    }

    /// <summary>Lists tracks on a specific album.</summary>
    [HttpGet("albums/{albumId:guid}/tracks")]
    public async Task<IActionResult> ListTracksByAlbum(Guid albumId)
    {
        var caller = GetAuthenticatedCaller();
        var tracks = await _trackService.ListTracksByAlbumAsync(albumId, caller);
        return Ok(Envelope(tracks));
    }

    /// <summary>Searches tracks by title.</summary>
    [HttpGet("tracks/search")]
    public async Task<IActionResult> SearchTracks([FromQuery] string q, [FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var tracks = await _trackService.SearchAsync(caller, q, take);
        return Ok(Envelope(tracks));
    }

    /// <summary>Gets random tracks.</summary>
    [HttpGet("tracks/random")]
    public async Task<IActionResult> GetRandomTracks([FromQuery] int take = 20, [FromQuery] string? genre = null)
    {
        var caller = GetAuthenticatedCaller();
        var tracks = await _trackService.GetRandomTracksAsync(caller, take, genre);
        return Ok(Envelope(tracks));
    }

    /// <summary>Gets recently added tracks.</summary>
    [HttpGet("tracks/recent")]
    public async Task<IActionResult> GetRecentTracks([FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var tracks = await _trackService.GetRecentTracksAsync(caller, take);
        return Ok(Envelope(tracks));
    }

    /// <summary>Deletes a track (soft delete).</summary>
    [HttpDelete("tracks/{trackId:guid}")]
    public async Task<IActionResult> DeleteTrack(Guid trackId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _trackService.DeleteTrackAsync(trackId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.TrackNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.TrackNotFound, ex.Message));
        }
    }

    // ─── Playlists ────────────────────────────────────────────────────

    /// <summary>Lists playlists for the current user.</summary>
    [HttpGet("playlists")]
    public async Task<IActionResult> ListPlaylists()
    {
        var caller = GetAuthenticatedCaller();
        var playlists = await _playlistService.ListPlaylistsAsync(caller);
        return Ok(Envelope(playlists));
    }

    /// <summary>Gets a playlist by ID.</summary>
    [HttpGet("playlists/{playlistId:guid}")]
    public async Task<IActionResult> GetPlaylist(Guid playlistId)
    {
        var caller = GetAuthenticatedCaller();
        var playlist = await _playlistService.GetPlaylistAsync(playlistId, caller);
        return playlist is null
            ? NotFound(ErrorEnvelope(ErrorCodes.PlaylistNotFound, "Playlist not found."))
            : Ok(Envelope(playlist));
    }

    /// <summary>Creates a new playlist.</summary>
    [HttpPost("playlists")]
    public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistDto dto)
    {
        var caller = GetAuthenticatedCaller();
        var playlist = await _playlistService.CreatePlaylistAsync(dto, caller);
        return Created($"/api/v1/music/playlists/{playlist.Id}", Envelope(playlist));
    }

    /// <summary>Updates a playlist.</summary>
    [HttpPut("playlists/{playlistId:guid}")]
    public async Task<IActionResult> UpdatePlaylist(Guid playlistId, [FromBody] UpdatePlaylistDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var playlist = await _playlistService.UpdatePlaylistAsync(playlistId, dto, caller);
            return Ok(Envelope(playlist));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.PlaylistNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PlaylistNotFound, ex.Message));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.MusicAccessDenied)
        {
            return StatusCode(403, ErrorEnvelope(ErrorCodes.MusicAccessDenied, ex.Message));
        }
    }

    /// <summary>Deletes a playlist (soft delete).</summary>
    [HttpDelete("playlists/{playlistId:guid}")]
    public async Task<IActionResult> DeletePlaylist(Guid playlistId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _playlistService.DeletePlaylistAsync(playlistId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.PlaylistNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PlaylistNotFound, ex.Message));
        }
    }

    /// <summary>Gets tracks in a playlist.</summary>
    [HttpGet("playlists/{playlistId:guid}/tracks")]
    public async Task<IActionResult> GetPlaylistTracks(Guid playlistId)
    {
        var caller = GetAuthenticatedCaller();
        var tracks = await _playlistService.GetPlaylistTracksAsync(playlistId, caller);
        return Ok(Envelope(tracks));
    }

    /// <summary>Adds a track to a playlist.</summary>
    [HttpPost("playlists/{playlistId:guid}/tracks/{trackId:guid}")]
    public async Task<IActionResult> AddTrackToPlaylist(Guid playlistId, Guid trackId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _playlistService.AddTrackAsync(playlistId, trackId, caller);
            return Ok(Envelope(new { added = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.TrackAlreadyInPlaylist)
        {
            return Conflict(ErrorEnvelope(ErrorCodes.TrackAlreadyInPlaylist, ex.Message));
        }
    }

    /// <summary>Removes a track from a playlist.</summary>
    [HttpDelete("playlists/{playlistId:guid}/tracks/{trackId:guid}")]
    public async Task<IActionResult> RemoveTrackFromPlaylist(Guid playlistId, Guid trackId)
    {
        var caller = GetAuthenticatedCaller();
        await _playlistService.RemoveTrackAsync(playlistId, trackId, caller);
        return Ok(Envelope(new { removed = true }));
    }

    // ─── Playback / Stars ─────────────────────────────────────────────

    /// <summary>Records a play event for a track.</summary>
    [HttpPost("tracks/{trackId:guid}/play")]
    public async Task<IActionResult> RecordPlay(Guid trackId)
    {
        var caller = GetAuthenticatedCaller();
        await _playbackService.RecordPlayAsync(trackId, 0, caller);
        return Ok(Envelope(new { recorded = true }));
    }

    /// <summary>Scrobbles a track.</summary>
    [HttpPost("tracks/{trackId:guid}/scrobble")]
    public async Task<IActionResult> Scrobble(Guid trackId)
    {
        var caller = GetAuthenticatedCaller();
        await _playbackService.ScrobbleAsync(trackId, caller);
        return Ok(Envelope(new { scrobbled = true }));
    }

    /// <summary>Toggles a star on a track.</summary>
    [HttpPost("tracks/{trackId:guid}/star")]
    public async Task<IActionResult> ToggleTrackStar(Guid trackId)
    {
        var caller = GetAuthenticatedCaller();
        await _playbackService.ToggleStarAsync(trackId, StarredItemType.Track, caller);
        return Ok(Envelope(new { toggled = true }));
    }

    /// <summary>Toggles a star on an album.</summary>
    [HttpPost("albums/{albumId:guid}/star")]
    public async Task<IActionResult> ToggleAlbumStar(Guid albumId)
    {
        var caller = GetAuthenticatedCaller();
        await _playbackService.ToggleStarAsync(albumId, StarredItemType.Album, caller);
        return Ok(Envelope(new { toggled = true }));
    }

    /// <summary>Toggles a star on an artist.</summary>
    [HttpPost("artists/{artistId:guid}/star")]
    public async Task<IActionResult> ToggleArtistStar(Guid artistId)
    {
        var caller = GetAuthenticatedCaller();
        await _playbackService.ToggleStarAsync(artistId, StarredItemType.Artist, caller);
        return Ok(Envelope(new { toggled = true }));
    }

    /// <summary>Gets recently played tracks.</summary>
    [HttpGet("recently-played")]
    public async Task<IActionResult> GetRecentlyPlayed([FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var tracks = await _playbackService.GetRecentlyPlayedAsync(caller.UserId, take);
        // Returns PlaybackHistory entities; map in caller if needed
        return Ok(Envelope(tracks));
    }

    /// <summary>Gets most played tracks.</summary>
    [HttpGet("most-played")]
    public async Task<IActionResult> GetMostPlayed([FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var tracks = await _playbackService.GetMostPlayedAsync(caller.UserId, take);
        // Returns Track entities ordered by PlayCount
        return Ok(Envelope(tracks));
    }

    /// <summary>Gets starred items by type.</summary>
    [HttpGet("starred")]
    public async Task<IActionResult> GetStarred([FromQuery] StarredItemType type = StarredItemType.Track)
    {
        var caller = GetAuthenticatedCaller();
        var items = await _playbackService.GetStarredAsync(caller.UserId, type);
        // Returns StarredItem entities
        return Ok(Envelope(items));
    }

    // ─── Recommendations ──────────────────────────────────────────────

    /// <summary>Gets track recommendations based on listening history.</summary>
    [HttpGet("recommendations/similar/{trackId:guid}")]
    public async Task<IActionResult> GetSimilarTracks(Guid trackId, [FromQuery] int take = 10)
    {
        var caller = GetAuthenticatedCaller();
        var tracks = await _recommendationService.GetSimilarTracksAsync(trackId, caller, take);
        return Ok(Envelope(tracks));
    }

    /// <summary>Gets newly added tracks.</summary>
    [HttpGet("recommendations/new")]
    public async Task<IActionResult> GetNewAdditions([FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var tracks = await _recommendationService.GetNewAdditionsAsync(caller, take);
        return Ok(Envelope(tracks));
    }

    /// <summary>Gets available genres.</summary>
    [HttpGet("genres")]
    public async Task<IActionResult> GetGenres()
    {
        var caller = GetAuthenticatedCaller();
        var genres = await _recommendationService.GetGenresAsync(caller.UserId);
        // Returns List<string> of genre names
        return Ok(Envelope(genres));
    }

    // ─── Equalizer ────────────────────────────────────────────────────

    /// <summary>Lists EQ presets (built-in + user custom).</summary>
    [HttpGet("eq/presets")]
    public async Task<IActionResult> ListEqPresets()
    {
        var caller = GetAuthenticatedCaller();
        var presets = await _eqPresetService.ListPresetsAsync(caller);
        return Ok(Envelope(presets));
    }

    /// <summary>Gets an EQ preset by ID.</summary>
    [HttpGet("eq/presets/{presetId:guid}")]
    public async Task<IActionResult> GetEqPreset(Guid presetId)
    {
        var caller = GetAuthenticatedCaller();
        var preset = await _eqPresetService.GetPresetAsync(presetId, caller);
        return preset is null
            ? NotFound(ErrorEnvelope(ErrorCodes.EqPresetNotFound, "EQ preset not found."))
            : Ok(Envelope(preset));
    }

    /// <summary>Creates a custom EQ preset.</summary>
    [HttpPost("eq/presets")]
    public async Task<IActionResult> CreateEqPreset([FromBody] SaveEqPresetDto dto)
    {
        var caller = GetAuthenticatedCaller();
        var preset = await _eqPresetService.CreatePresetAsync(dto, caller);
        return Created($"/api/v1/music/eq/presets/{preset.Id}", Envelope(preset));
    }

    /// <summary>Updates a custom EQ preset.</summary>
    [HttpPut("eq/presets/{presetId:guid}")]
    public async Task<IActionResult> UpdateEqPreset(Guid presetId, [FromBody] SaveEqPresetDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var preset = await _eqPresetService.UpdatePresetAsync(presetId, dto, caller);
            return Ok(Envelope(preset));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.EqPresetNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.EqPresetNotFound, ex.Message));
        }
    }

    /// <summary>Deletes a custom EQ preset.</summary>
    [HttpDelete("eq/presets/{presetId:guid}")]
    public async Task<IActionResult> DeleteEqPreset(Guid presetId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _eqPresetService.DeletePresetAsync(presetId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.EqPresetNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.EqPresetNotFound, ex.Message));
        }
    }

    /// <summary>Sets the active EQ preset for the current user.</summary>
    [HttpPost("eq/active/{presetId:guid}")]
    public async Task<IActionResult> SetActiveEqPreset(Guid presetId)
    {
        var caller = GetAuthenticatedCaller();
        await _eqPresetService.SetActivePresetAsync(presetId, caller);
        return Ok(Envelope(new { activePresetId = presetId }));
    }
}
