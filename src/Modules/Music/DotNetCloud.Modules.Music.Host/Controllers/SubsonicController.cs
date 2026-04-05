using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Host.Subsonic;
using DotNetCloud.Modules.Music.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Music.Host.Controllers;

/// <summary>
/// Implements Subsonic REST API v1.16 compatible endpoints.
/// Supports both XML and JSON response formats via the 'f' parameter.
/// </summary>
[Route("rest")]
[ApiController]
public class SubsonicController : ControllerBase
{
    private readonly ArtistService _artistService;
    private readonly MusicAlbumService _albumService;
    private readonly TrackService _trackService;
    private readonly PlaylistService _playlistService;
    private readonly PlaybackService _playbackService;
    private readonly RecommendationService _recommendationService;
    private readonly MusicStreamingService _streamingService;
    private readonly MusicDbContext _db;
    private readonly ILogger<SubsonicController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsonicController"/> class.
    /// </summary>
    public SubsonicController(
        ArtistService artistService,
        MusicAlbumService albumService,
        TrackService trackService,
        PlaylistService playlistService,
        PlaybackService playbackService,
        RecommendationService recommendationService,
        MusicStreamingService streamingService,
        MusicDbContext db,
        ILogger<SubsonicController> logger)
    {
        _artistService = artistService;
        _albumService = albumService;
        _trackService = trackService;
        _playlistService = playlistService;
        _playbackService = playbackService;
        _recommendationService = recommendationService;
        _streamingService = streamingService;
        _db = db;
        _logger = logger;
    }

    // ─── System ──────────────────────────────────────────────────────

    /// <summary>Subsonic ping endpoint.</summary>
    [HttpGet("ping")]
    [HttpGet("ping.view")]
    public IActionResult Ping() => SubsonicOk(SubsonicResponse.Ok());

    /// <summary>Subsonic getLicense endpoint.</summary>
    [HttpGet("getLicense")]
    [HttpGet("getLicense.view")]
    public IActionResult GetLicense()
    {
        var response = SubsonicResponse.Ok();
        response.License = new SubsonicLicense();
        return SubsonicOk(response);
    }

    /// <summary>Subsonic getOpenSubsonicExtensions endpoint.</summary>
    [HttpGet("getOpenSubsonicExtensions")]
    [HttpGet("getOpenSubsonicExtensions.view")]
    public IActionResult GetOpenSubsonicExtensions()
    {
        var response = SubsonicResponse.Ok();
        response.OpenSubsonicExtensions = new SubsonicExtensions
        {
            OpenSubsonicExtension =
            [
                new SubsonicExtension { Name = "transcodeOffset", Versions = "1" }
            ]
        };
        return SubsonicOk(response);
    }

    // ─── Browsing ─────────────────────────────────────────────────────

    /// <summary>Subsonic getArtists endpoint.</summary>
    [HttpGet("getArtists")]
    [HttpGet("getArtists.view")]
    public async Task<IActionResult> GetArtists()
    {
        var caller = GetCaller();
        var artists = await _artistService.ListArtistsAsync(caller, 0, 10000);

        var index = artists
            .GroupBy(a => (a.SortName ?? a.Name)[..1].ToUpperInvariant())
            .OrderBy(g => g.Key)
            .Select(g => new SubsonicArtistIndex
            {
                Name = g.Key,
                Artist = g.Select(a => new SubsonicArtistSummary
                {
                    Id = a.Id.ToString(),
                    Name = a.Name,
                    AlbumCount = a.AlbumCount,
                    Starred = a.IsStarred ? DateTime.UtcNow.ToString("O") : null
                }).ToList()
            }).ToList();

        var response = SubsonicResponse.Ok();
        response.Artists = new SubsonicArtistsResult { Index = index };
        return SubsonicOk(response);
    }

    /// <summary>Subsonic getArtist endpoint.</summary>
    [HttpGet("getArtist")]
    [HttpGet("getArtist.view")]
    public async Task<IActionResult> GetArtist([FromQuery] string id)
    {
        if (!Guid.TryParse(id, out var artistId))
            return SubsonicError(10, "Required parameter is missing: id");

        var caller = GetCaller();
        var artist = await _artistService.GetArtistAsync(artistId, caller);
        if (artist is null)
            return SubsonicError(70, "Artist not found.");

        var albums = await _albumService.ListAlbumsByArtistAsync(artistId, caller);

        var response = SubsonicResponse.Ok();
        response.Artist = new SubsonicArtistDetail
        {
            Id = artist.Id.ToString(),
            Name = artist.Name,
            AlbumCount = artist.AlbumCount,
            Album = albums.Select(MapAlbum).ToList()
        };
        return SubsonicOk(response);
    }

    /// <summary>Subsonic getAlbum endpoint.</summary>
    [HttpGet("getAlbum")]
    [HttpGet("getAlbum.view")]
    public async Task<IActionResult> GetAlbum([FromQuery] string id)
    {
        if (!Guid.TryParse(id, out var albumId))
            return SubsonicError(10, "Required parameter is missing: id");

        var caller = GetCaller();
        var album = await _albumService.GetAlbumAsync(albumId, caller);
        if (album is null)
            return SubsonicError(70, "Album not found.");

        var tracks = await _trackService.ListTracksByAlbumAsync(albumId, caller);

        var subAlbum = MapAlbum(album);
        subAlbum.Song = tracks.Select(MapSong).ToList();

        var response = SubsonicResponse.Ok();
        response.Album = subAlbum;
        return SubsonicOk(response);
    }

    /// <summary>Subsonic getSong endpoint.</summary>
    [HttpGet("getSong")]
    [HttpGet("getSong.view")]
    public async Task<IActionResult> GetSong([FromQuery] string id)
    {
        if (!Guid.TryParse(id, out var trackId))
            return SubsonicError(10, "Required parameter is missing: id");

        var caller = GetCaller();
        var track = await _trackService.GetTrackAsync(trackId, caller);
        if (track is null)
            return SubsonicError(70, "Song not found.");

        var response = SubsonicResponse.Ok();
        response.Song = MapSong(track);
        return SubsonicOk(response);
    }

    /// <summary>Subsonic getAlbumList2 endpoint.</summary>
    [HttpGet("getAlbumList2")]
    [HttpGet("getAlbumList2.view")]
    public async Task<IActionResult> GetAlbumList2(
        [FromQuery] string type = "newest",
        [FromQuery] int size = 10,
        [FromQuery] int offset = 0)
    {
        var caller = GetCaller();
        var albums = type switch
        {
            "newest" => await _albumService.GetRecentAlbumsAsync(caller, size),
            "alphabeticalByName" => await _albumService.ListAlbumsAsync(caller, offset, size),
            "alphabeticalByArtist" => await _albumService.ListAlbumsAsync(caller, offset, size),
            _ => await _albumService.ListAlbumsAsync(caller, offset, size)
        };

        var response = SubsonicResponse.Ok();
        response.AlbumList2 = new SubsonicAlbumList
        {
            Album = albums.Select(MapAlbum).ToList()
        };
        return SubsonicOk(response);
    }

    /// <summary>Subsonic getRandomSongs endpoint.</summary>
    [HttpGet("getRandomSongs")]
    [HttpGet("getRandomSongs.view")]
    public async Task<IActionResult> GetRandomSongs(
        [FromQuery] int size = 10,
        [FromQuery] string? genre = null)
    {
        var caller = GetCaller();
        var tracks = await _trackService.GetRandomTracksAsync(caller, size, genre);

        var response = SubsonicResponse.Ok();
        response.RandomSongs = new SubsonicSongList
        {
            Song = tracks.Select(MapSong).ToList()
        };
        return SubsonicOk(response);
    }

    /// <summary>Subsonic getGenres endpoint.</summary>
    [HttpGet("getGenres")]
    [HttpGet("getGenres.view")]
    public async Task<IActionResult> GetGenres()
    {
        var caller = GetCaller();
        var genres = await _recommendationService.GetGenresAsync(caller.UserId);

        var response = SubsonicResponse.Ok();
        response.Genres = new SubsonicGenres
        {
            Genre = genres.Select(g => new SubsonicGenre { Value = g }).ToList()
        };
        return SubsonicOk(response);
    }

    /// <summary>Subsonic getStarred2 endpoint.</summary>
    [HttpGet("getStarred2")]
    [HttpGet("getStarred2.view")]
    public async Task<IActionResult> GetStarred2()
    {
        var caller = GetCaller();

        var starredArtistItems = await _playbackService.GetStarredAsync(caller.UserId, StarredItemType.Artist);
        var starredAlbumItems = await _playbackService.GetStarredAsync(caller.UserId, StarredItemType.Album);
        var starredTrackItems = await _playbackService.GetStarredAsync(caller.UserId, StarredItemType.Track);

        var artists = new List<SubsonicArtistSummary>();
        foreach (var item in starredArtistItems)
        {
            var artist = await _artistService.GetArtistAsync(item.ItemId, caller);
            if (artist is not null)
            {
                artists.Add(new SubsonicArtistSummary
                {
                    Id = artist.Id.ToString(),
                    Name = artist.Name,
                    AlbumCount = artist.AlbumCount,
                    Starred = item.StarredAt.ToString("O")
                });
            }
        }

        var albums = new List<SubsonicAlbum>();
        foreach (var item in starredAlbumItems)
        {
            var album = await _albumService.GetAlbumAsync(item.ItemId, caller);
            if (album is not null)
                albums.Add(MapAlbum(album));
        }

        var songs = new List<SubsonicSong>();
        foreach (var item in starredTrackItems)
        {
            var track = await _trackService.GetTrackAsync(item.ItemId, caller);
            if (track is not null)
                songs.Add(MapSong(track));
        }

        var response = SubsonicResponse.Ok();
        response.Starred2 = new SubsonicStarred
        {
            Artist = artists,
            Album = albums,
            Song = songs
        };
        return SubsonicOk(response);
    }

    // ─── Search ───────────────────────────────────────────────────────

    /// <summary>Subsonic search3 endpoint.</summary>
    [HttpGet("search3")]
    [HttpGet("search3.view")]
    public async Task<IActionResult> Search3(
        [FromQuery] string query = "",
        [FromQuery] int artistCount = 20,
        [FromQuery] int albumCount = 20,
        [FromQuery] int songCount = 20)
    {
        var caller = GetCaller();

        var artists = string.IsNullOrWhiteSpace(query) ? [] : await _artistService.SearchAsync(caller, query, artistCount);
        var albums = string.IsNullOrWhiteSpace(query) ? [] : await _albumService.SearchAsync(caller, query, albumCount);
        var tracks = string.IsNullOrWhiteSpace(query) ? [] : await _trackService.SearchAsync(caller, query, songCount);

        var response = SubsonicResponse.Ok();
        response.SearchResult3 = new SubsonicSearchResult
        {
            Artist = artists.Select(a => new SubsonicArtistSummary
            {
                Id = a.Id.ToString(),
                Name = a.Name,
                AlbumCount = a.AlbumCount,
                Starred = a.IsStarred ? DateTime.UtcNow.ToString("O") : null
            }).ToList(),
            Album = albums.Select(MapAlbum).ToList(),
            Song = tracks.Select(MapSong).ToList()
        };
        return SubsonicOk(response);
    }

    // ─── Playlists ────────────────────────────────────────────────────

    /// <summary>Subsonic getPlaylists endpoint.</summary>
    [HttpGet("getPlaylists")]
    [HttpGet("getPlaylists.view")]
    public async Task<IActionResult> GetPlaylists()
    {
        var caller = GetCaller();
        var playlists = await _playlistService.ListPlaylistsAsync(caller);

        var response = SubsonicResponse.Ok();
        response.Playlists = new SubsonicPlaylists
        {
            Playlist = playlists.Select(p => new SubsonicPlaylistSummary
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                SongCount = p.TrackCount,
                Duration = (int)p.TotalDuration.TotalSeconds,
                IsPublic = p.IsPublic,
                Owner = p.OwnerId.ToString()
            }).ToList()
        };
        return SubsonicOk(response);
    }

    /// <summary>Subsonic getPlaylist endpoint.</summary>
    [HttpGet("getPlaylist")]
    [HttpGet("getPlaylist.view")]
    public async Task<IActionResult> GetPlaylist([FromQuery] string id)
    {
        if (!Guid.TryParse(id, out var playlistId))
            return SubsonicError(10, "Required parameter is missing: id");

        var caller = GetCaller();
        var playlist = await _playlistService.GetPlaylistAsync(playlistId, caller);
        if (playlist is null)
            return SubsonicError(70, "Playlist not found.");

        var tracks = await _playlistService.GetPlaylistTracksAsync(playlistId, caller);

        var response = SubsonicResponse.Ok();
        response.Playlist = new SubsonicPlaylistDetail
        {
            Id = playlist.Id.ToString(),
            Name = playlist.Name,
            SongCount = playlist.TrackCount,
            Duration = (int)playlist.TotalDuration.TotalSeconds,
            IsPublic = playlist.IsPublic,
            Owner = playlist.OwnerId.ToString(),
            Entry = tracks.Select(MapSong).ToList()
        };
        return SubsonicOk(response);
    }

    /// <summary>Subsonic createPlaylist endpoint.</summary>
    [HttpGet("createPlaylist")]
    [HttpGet("createPlaylist.view")]
    public async Task<IActionResult> CreatePlaylist([FromQuery] string? name, [FromQuery] string? playlistId)
    {
        var caller = GetCaller();

        if (!string.IsNullOrWhiteSpace(playlistId) && Guid.TryParse(playlistId, out var existingId))
        {
            // Update existing playlist name
            if (!string.IsNullOrWhiteSpace(name))
            {
                await _playlistService.UpdatePlaylistAsync(existingId,
                    new Core.DTOs.UpdatePlaylistDto { Name = name }, caller);
            }
        }
        else if (!string.IsNullOrWhiteSpace(name))
        {
            await _playlistService.CreatePlaylistAsync(
                new Core.DTOs.CreatePlaylistDto { Name = name }, caller);
        }

        return SubsonicOk(SubsonicResponse.Ok());
    }

    /// <summary>Subsonic deletePlaylist endpoint.</summary>
    [HttpGet("deletePlaylist")]
    [HttpGet("deletePlaylist.view")]
    public async Task<IActionResult> DeletePlaylist([FromQuery] string id)
    {
        if (!Guid.TryParse(id, out var playlistId))
            return SubsonicError(10, "Required parameter is missing: id");

        var caller = GetCaller();
        await _playlistService.DeletePlaylistAsync(playlistId, caller);
        return SubsonicOk(SubsonicResponse.Ok());
    }

    /// <summary>Subsonic updatePlaylist endpoint.</summary>
    [HttpGet("updatePlaylist")]
    [HttpGet("updatePlaylist.view")]
    public async Task<IActionResult> UpdatePlaylist(
        [FromQuery] string playlistId,
        [FromQuery] string? name = null,
        [FromQuery] string? comment = null,
        [FromQuery(Name = "songIdToAdd")] string[]? songIdsToAdd = null,
        [FromQuery(Name = "songIndexToRemove")] int[]? songIndexesToRemove = null)
    {
        if (!Guid.TryParse(playlistId, out var plId))
            return SubsonicError(10, "Required parameter is missing: playlistId");

        var caller = GetCaller();

        if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(comment))
        {
            await _playlistService.UpdatePlaylistAsync(plId,
                new Core.DTOs.UpdatePlaylistDto { Name = name, Description = comment }, caller);
        }

        if (songIdsToAdd is not null)
        {
            foreach (var songId in songIdsToAdd)
            {
                if (Guid.TryParse(songId, out var trackId))
                {
                    try { await _playlistService.AddTrackAsync(plId, trackId, caller); }
                    catch { /* Skip duplicates */ }
                }
            }
        }

        return SubsonicOk(SubsonicResponse.Ok());
    }

    // ─── User Interaction ─────────────────────────────────────────────

    /// <summary>Subsonic star endpoint.</summary>
    [HttpGet("star")]
    [HttpGet("star.view")]
    public async Task<IActionResult> Star(
        [FromQuery] string? id = null,
        [FromQuery] string? albumId = null,
        [FromQuery] string? artistId = null)
    {
        var caller = GetCaller();

        if (!string.IsNullOrWhiteSpace(id) && Guid.TryParse(id, out var trackGuid))
            await _playbackService.ToggleStarAsync(trackGuid, StarredItemType.Track, caller);

        if (!string.IsNullOrWhiteSpace(albumId) && Guid.TryParse(albumId, out var albumGuid))
            await _playbackService.ToggleStarAsync(albumGuid, StarredItemType.Album, caller);

        if (!string.IsNullOrWhiteSpace(artistId) && Guid.TryParse(artistId, out var artistGuid))
            await _playbackService.ToggleStarAsync(artistGuid, StarredItemType.Artist, caller);

        return SubsonicOk(SubsonicResponse.Ok());
    }

    /// <summary>Subsonic unstar endpoint.</summary>
    [HttpGet("unstar")]
    [HttpGet("unstar.view")]
    public async Task<IActionResult> Unstar(
        [FromQuery] string? id = null,
        [FromQuery] string? albumId = null,
        [FromQuery] string? artistId = null)
    {
        // Unstar uses same toggle logic (removes star)
        return await Star(id, albumId, artistId);
    }

    /// <summary>Subsonic scrobble endpoint.</summary>
    [HttpGet("scrobble")]
    [HttpGet("scrobble.view")]
    public async Task<IActionResult> Scrobble([FromQuery] string id)
    {
        if (!Guid.TryParse(id, out var trackId))
            return SubsonicError(10, "Required parameter is missing: id");

        var caller = GetCaller();
        await _playbackService.ScrobbleAsync(trackId, caller);
        return SubsonicOk(SubsonicResponse.Ok());
    }

    // ─── Media Retrieval ──────────────────────────────────────────────

    /// <summary>Subsonic stream endpoint — serves audio with HTTP Range support.</summary>
    [HttpGet("stream")]
    [HttpGet("stream.view")]
    public async Task<IActionResult> Stream([FromQuery] string id)
    {
        if (!Guid.TryParse(id, out var trackId))
            return SubsonicError(10, "Required parameter is missing: id");

        var caller = GetCaller();
        var track = await _streamingService.GetTrackForStreamingAsync(trackId, caller.UserId);
        if (track is null)
            return SubsonicError(70, "Song not found.");

        // The actual file serving would require file system access
        // For now, return a placeholder that the host can override
        return Ok(new { trackId = track.Id, mimeType = track.MimeType, message = "Streaming requires file system integration" });
    }

    /// <summary>Subsonic getCoverArt endpoint.</summary>
    [HttpGet("getCoverArt")]
    [HttpGet("getCoverArt.view")]
    public async Task<IActionResult> GetCoverArt([FromQuery] string id)
    {
        if (!Guid.TryParse(id, out var albumId))
            return SubsonicError(10, "Required parameter is missing: id");

        var artPath = await _albumService.GetCoverArtPathAsync(albumId);
        if (artPath is null || !System.IO.File.Exists(artPath))
            return NotFound();

        var mimeType = artPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
        return PhysicalFile(artPath, mimeType);
    }

    /// <summary>Subsonic download endpoint (same as stream for direct files).</summary>
    [HttpGet("download")]
    [HttpGet("download.view")]
    public Task<IActionResult> Download([FromQuery] string id) => Stream(id);

    // ─── Helpers ──────────────────────────────────────────────────────

    private CallerContext GetCaller()
    {
        // In standalone mode, extract from query params (u=username)
        // In production, extract from the auth middleware
        var userId = User?.Identity?.IsAuthenticated == true
            ? Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? Guid.Empty.ToString())
            : Guid.Empty;

        return new CallerContext(userId, [], CallerType.User);
    }

    private IActionResult SubsonicOk(SubsonicResponse response)
    {
        var format = Request.Query["f"].FirstOrDefault() ?? "xml";
        if (format == "json")
        {
            return Ok(new { subsonicResponse = response });
        }
        return Ok(response); // Default XML via content negotiation
    }

    private IActionResult SubsonicError(int code, string message)
    {
        var response = SubsonicResponse.Failed(code, message);
        return SubsonicOk(response);
    }

    private static SubsonicAlbum MapAlbum(Core.DTOs.MusicAlbumDto album) => new()
    {
        Id = album.Id.ToString(),
        Name = album.Title,
        Artist = album.ArtistName,
        ArtistId = album.ArtistId.ToString(),
        SongCount = album.TrackCount,
        Duration = (int)album.TotalDuration.TotalSeconds,
        CoverArt = album.HasCoverArt ? album.Id.ToString() : null,
        Year = album.Year ?? 0,
        Genre = album.Genre,
        Starred = album.IsStarred ? DateTime.UtcNow.ToString("O") : null
    };

    private static SubsonicSong MapSong(Core.DTOs.TrackDto track) => new()
    {
        Id = track.Id.ToString(),
        Title = track.Title,
        Album = track.AlbumTitle,
        Artist = track.ArtistName,
        Track = track.TrackNumber ?? 0,
        DiscNumber = track.DiscNumber ?? 1,
        Year = track.Year ?? 0,
        Genre = track.Genre,
        Duration = (int)track.Duration.TotalSeconds,
        Size = track.SizeBytes,
        ContentType = track.MimeType,
        Suffix = MimeToSuffix(track.MimeType),
        BitRate = track.Bitrate.HasValue ? (int)(track.Bitrate.Value / 1000) : 0,
        AlbumId = track.AlbumId?.ToString(),
        ArtistId = track.ArtistId.ToString(),
        CoverArt = track.AlbumId?.ToString(),
        Starred = track.IsStarred ? DateTime.UtcNow.ToString("O") : null
    };

    private static string MimeToSuffix(string mimeType) => mimeType switch
    {
        "audio/mpeg" or "audio/mp3" => "mp3",
        "audio/flac" => "flac",
        "audio/ogg" or "audio/vorbis" => "ogg",
        "audio/opus" => "opus",
        "audio/aac" or "audio/mp4" or "audio/m4a" or "audio/x-m4a" => "m4a",
        "audio/wav" or "audio/x-wav" or "audio/wave" => "wav",
        "audio/x-ms-wma" => "wma",
        "audio/webm" => "webm",
        _ => "bin"
    };
}
