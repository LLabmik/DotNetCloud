using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Host.Protos;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Grpc.Core;

namespace DotNetCloud.Modules.Music.Host.Services;

/// <summary>
/// gRPC service implementation for the Music module.
/// Exposes music operations over gRPC for the core server to invoke.
/// </summary>
public sealed class MusicGrpcServiceImpl : MusicGrpcService.MusicGrpcServiceBase
{
    private readonly ArtistService _artistService;
    private readonly MusicAlbumService _albumService;
    private readonly TrackService _trackService;
    private readonly PlaylistService _playlistService;
    private readonly PlaybackService _playbackService;
    private readonly ILogger<MusicGrpcServiceImpl> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicGrpcServiceImpl"/> class.
    /// </summary>
    public MusicGrpcServiceImpl(
        ArtistService artistService,
        MusicAlbumService albumService,
        TrackService trackService,
        PlaylistService playlistService,
        PlaybackService playbackService,
        ILogger<MusicGrpcServiceImpl> logger)
    {
        _artistService = artistService;
        _albumService = albumService;
        _trackService = trackService;
        _playlistService = playlistService;
        _playbackService = playbackService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ArtistResponse> GetArtist(GetArtistRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var artist = await _artistService.GetArtistAsync(Guid.Parse(request.ArtistId), caller);
            if (artist is null)
                return new ArtistResponse { Success = false, ErrorMessage = "Artist not found." };
            return new ArtistResponse { Success = true, Artist = MapArtist(artist) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetArtist failed");
            return new ArtistResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListArtistsResponse> ListArtists(ListArtistsRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var artists = await _artistService.ListArtistsAsync(caller, request.Skip, request.Take);
            var response = new ListArtistsResponse { Success = true };
            foreach (var a in artists)
                response.Artists.Add(MapArtist(a));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListArtists failed");
            return new ListArtistsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<AlbumResponse> GetAlbum(GetAlbumRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var album = await _albumService.GetAlbumAsync(Guid.Parse(request.AlbumId), caller);
            if (album is null)
                return new AlbumResponse { Success = false, ErrorMessage = "Album not found." };
            return new AlbumResponse { Success = true, Album = MapAlbum(album) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAlbum failed");
            return new AlbumResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListAlbumsResponse> ListAlbums(ListAlbumsRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var albums = await _albumService.ListAlbumsAsync(caller, request.Skip, request.Take);
            var response = new ListAlbumsResponse { Success = true };
            foreach (var a in albums)
                response.Albums.Add(MapAlbum(a));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListAlbums failed");
            return new ListAlbumsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<TrackResponse> GetTrack(GetTrackRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var track = await _trackService.GetTrackAsync(Guid.Parse(request.TrackId), caller);
            if (track is null)
                return new TrackResponse { Success = false, ErrorMessage = "Track not found." };
            return new TrackResponse { Success = true, Track = MapTrack(track) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTrack failed");
            return new TrackResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListTracksResponse> ListTracks(ListTracksRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var tracks = await _trackService.ListTracksAsync(caller, request.Skip, request.Take);
            var response = new ListTracksResponse { Success = true };
            foreach (var t in tracks)
                response.Tracks.Add(MapTrack(t));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListTracks failed");
            return new ListTracksResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListTracksResponse> ListTracksByAlbum(ListTracksByAlbumRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var tracks = await _trackService.ListTracksByAlbumAsync(Guid.Parse(request.AlbumId), caller);
            var response = new ListTracksResponse { Success = true };
            foreach (var t in tracks)
                response.Tracks.Add(MapTrack(t));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListTracksByAlbum failed");
            return new ListTracksResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<PlaylistResponse> GetPlaylist(GetPlaylistRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var playlist = await _playlistService.GetPlaylistAsync(Guid.Parse(request.PlaylistId), caller);
            if (playlist is null)
                return new PlaylistResponse { Success = false, ErrorMessage = "Playlist not found." };
            return new PlaylistResponse { Success = true, Playlist = MapPlaylist(playlist) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPlaylist failed");
            return new PlaylistResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListPlaylistsResponse> ListPlaylists(ListPlaylistsRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var playlists = await _playlistService.ListPlaylistsAsync(caller);
            var response = new ListPlaylistsResponse { Success = true };
            foreach (var p in playlists)
                response.Playlists.Add(MapPlaylist(p));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListPlaylists failed");
            return new ListPlaylistsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<PlaylistResponse> CreatePlaylist(CreatePlaylistRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var dto = new CreatePlaylistDto
            {
                Name = request.Name,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                IsPublic = request.IsPublic
            };
            var playlist = await _playlistService.CreatePlaylistAsync(dto, caller);
            return new PlaylistResponse { Success = true, Playlist = MapPlaylist(playlist) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreatePlaylist failed");
            return new PlaylistResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> DeletePlaylist(DeletePlaylistRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            await _playlistService.DeletePlaylistAsync(Guid.Parse(request.PlaylistId), caller);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeletePlaylist failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> RecordPlay(RecordPlayRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            await _playbackService.RecordPlayAsync(Guid.Parse(request.TrackId), 0, caller);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecordPlay failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> Scrobble(ScrobbleRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            await _playbackService.ScrobbleAsync(Guid.Parse(request.TrackId), caller);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scrobble failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> ToggleStar(ToggleStarRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var itemType = Enum.Parse<StarredItemType>(request.ItemType, ignoreCase: true);
            await _playbackService.ToggleStarAsync(Guid.Parse(request.ItemId), itemType, caller);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ToggleStar failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<SearchResponse> Search(SearchRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var artists = await _artistService.SearchAsync(caller, request.Query, request.ArtistCount);
            var albums = await _albumService.SearchAsync(caller, request.Query, request.AlbumCount);
            var tracks = await _trackService.SearchAsync(caller, request.Query, request.TrackCount);

            var response = new SearchResponse { Success = true };
            foreach (var a in artists) response.Artists.Add(MapArtist(a));
            foreach (var a in albums) response.Albums.Add(MapAlbum(a));
            foreach (var t in tracks) response.Tracks.Add(MapTrack(t));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed");
            return new SearchResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ── Mapping helpers ─────────────────────────────────────────────

    private static CallerContext ParseCaller(string userId)
    {
        return new CallerContext(Guid.Parse(userId), [], CallerType.Module);
    }

    private static ArtistMessage MapArtist(ArtistDto dto)
    {
        return new ArtistMessage
        {
            Id = dto.Id.ToString(),
            Name = dto.Name,
            SortName = dto.SortName ?? "",
            AlbumCount = dto.AlbumCount,
            TrackCount = dto.TrackCount,
            IsStarred = dto.IsStarred,
            CreatedAt = dto.CreatedAt.ToString("O")
        };
    }

    private static AlbumMessage MapAlbum(MusicAlbumDto dto)
    {
        return new AlbumMessage
        {
            Id = dto.Id.ToString(),
            Title = dto.Title,
            ArtistId = dto.ArtistId.ToString(),
            ArtistName = dto.ArtistName,
            Year = dto.Year ?? 0,
            Genre = dto.Genre ?? "",
            TrackCount = dto.TrackCount,
            TotalDurationTicks = dto.TotalDuration.Ticks,
            HasCoverArt = dto.HasCoverArt,
            IsStarred = dto.IsStarred,
            CreatedAt = dto.CreatedAt.ToString("O")
        };
    }

    private static TrackMessage MapTrack(TrackDto dto)
    {
        return new TrackMessage
        {
            Id = dto.Id.ToString(),
            FileNodeId = dto.FileNodeId.ToString(),
            Title = dto.Title,
            TrackNumber = dto.TrackNumber ?? 0,
            DiscNumber = dto.DiscNumber ?? 1,
            DurationTicks = dto.Duration.Ticks,
            SizeBytes = dto.SizeBytes,
            Bitrate = dto.Bitrate ?? 0,
            MimeType = dto.MimeType,
            AlbumId = dto.AlbumId?.ToString() ?? "",
            AlbumTitle = dto.AlbumTitle ?? "",
            ArtistId = dto.ArtistId.ToString(),
            ArtistName = dto.ArtistName,
            Genre = dto.Genre ?? "",
            Year = dto.Year ?? 0,
            IsStarred = dto.IsStarred,
            CreatedAt = dto.CreatedAt.ToString("O")
        };
    }

    private static PlaylistMessage MapPlaylist(PlaylistDto dto)
    {
        return new PlaylistMessage
        {
            Id = dto.Id.ToString(),
            OwnerId = dto.OwnerId.ToString(),
            Name = dto.Name,
            Description = dto.Description ?? "",
            IsPublic = dto.IsPublic,
            TrackCount = dto.TrackCount,
            TotalDurationTicks = dto.TotalDuration.Ticks,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O")
        };
    }
}
