using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Manages video CRUD and query operations.
/// </summary>
public interface IVideoService
{
    /// <summary>Creates a video record from an uploaded file.</summary>
    Task<VideoDto> CreateVideoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a video by ID.</summary>
    Task<VideoDto?> GetVideoAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a video by its Files-module FileNodeId.</summary>
    Task<VideoDto?> GetVideoByFileNodeIdAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists videos with paging.</summary>
    Task<IReadOnlyList<VideoDto>> ListVideosAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets the total video count for a user.</summary>
    Task<int> GetVideoCountAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Searches videos by query.</summary>
    Task<IReadOnlyList<VideoDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default);

    /// <summary>Gets recently added videos with paging.</summary>
    Task<IReadOnlyList<VideoDto>> GetRecentVideosAsync(CallerContext caller, int skip = 0, int take = 20, CancellationToken cancellationToken = default);

    /// <summary>Gets favorited videos.</summary>
    Task<IReadOnlyList<VideoDto>> GetFavoritesAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Toggles the favorite flag on a video.</summary>
    Task<bool> ToggleFavoriteAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes a video.</summary>
    Task DeleteVideoAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);
}
