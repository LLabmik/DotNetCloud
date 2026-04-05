using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Manages user playlists.
/// </summary>
public interface IPlaylistService
{
    /// <summary>Creates a new playlist.</summary>
    Task<PlaylistDto> CreatePlaylistAsync(CreatePlaylistDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a playlist by ID.</summary>
    Task<PlaylistDto?> GetPlaylistAsync(Guid playlistId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all playlists for the caller.</summary>
    Task<IReadOnlyList<PlaylistDto>> ListPlaylistsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates a playlist.</summary>
    Task<PlaylistDto> UpdatePlaylistAsync(Guid playlistId, UpdatePlaylistDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes a playlist.</summary>
    Task DeletePlaylistAsync(Guid playlistId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Adds a track to a playlist.</summary>
    Task AddTrackAsync(Guid playlistId, Guid trackId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a track from a playlist.</summary>
    Task RemoveTrackAsync(Guid playlistId, Guid trackId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all tracks in a playlist.</summary>
    Task<IReadOnlyList<TrackDto>> GetPlaylistTracksAsync(Guid playlistId, CallerContext caller, CancellationToken cancellationToken = default);
}
