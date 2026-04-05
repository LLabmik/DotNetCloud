using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Manages video collections (playlists/series).
/// </summary>
public interface IVideoCollectionService
{
    /// <summary>Creates a new video collection.</summary>
    Task<VideoCollectionDto> CreateCollectionAsync(CreateVideoCollectionDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a collection by ID.</summary>
    Task<VideoCollectionDto?> GetCollectionAsync(Guid collectionId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all collections for the caller.</summary>
    Task<IReadOnlyList<VideoCollectionDto>> ListCollectionsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates a collection.</summary>
    Task<VideoCollectionDto> UpdateCollectionAsync(Guid collectionId, UpdateVideoCollectionDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes a collection.</summary>
    Task DeleteCollectionAsync(Guid collectionId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Adds a video to a collection.</summary>
    Task AddVideoAsync(Guid collectionId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a video from a collection.</summary>
    Task RemoveVideoAsync(Guid collectionId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all videos in a collection.</summary>
    Task<IReadOnlyList<VideoDto>> GetCollectionVideosAsync(Guid collectionId, CallerContext caller, CancellationToken cancellationToken = default);
}
