using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// gRPC API client interface for the Video module.
/// </summary>
public interface IVideoApiClient
{
    /// <summary>Gets the current watch progress for a video (for resume playback).</summary>
    Task<WatchProgressDto?> GetWatchProgressAsync(Guid videoId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Updates watch progress for a video during playback.</summary>
    Task<bool> UpdateWatchProgressAsync(Guid videoId, long positionTicks, Guid userId, CancellationToken cancellationToken = default);
}
