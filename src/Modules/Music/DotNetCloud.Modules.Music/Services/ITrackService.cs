using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Manages music tracks.
/// </summary>
public interface ITrackService
{
    /// <summary>Gets a track by ID.</summary>
    Task<TrackDto?> GetTrackAsync(Guid trackId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists tracks with paging.</summary>
    Task<IReadOnlyList<TrackDto>> ListTracksAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Lists tracks for a specific album.</summary>
    Task<IReadOnlyList<TrackDto>> ListTracksByAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Searches tracks by query.</summary>
    Task<IReadOnlyList<TrackDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default);

    /// <summary>Gets recently added tracks.</summary>
    Task<IReadOnlyList<TrackDto>> GetRecentTracksAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default);

    /// <summary>Gets random tracks, optionally filtered by genre.</summary>
    Task<IReadOnlyList<TrackDto>> GetRandomTracksAsync(CallerContext caller, int count = 20, string? genre = null, CancellationToken cancellationToken = default);

    /// <summary>Gets starred (favorited) tracks for the current user.</summary>
    Task<IReadOnlyList<TrackDto>> GetStarredTracksAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes a track.</summary>
    Task DeleteTrackAsync(Guid trackId, CallerContext caller, CancellationToken cancellationToken = default);
}
