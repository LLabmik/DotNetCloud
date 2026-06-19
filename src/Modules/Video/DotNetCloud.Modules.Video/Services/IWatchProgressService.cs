using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Manages video watch progress for resume playback.
/// </summary>
public interface IWatchProgressService
{
    /// <summary>Gets the current watch progress for a video.</summary>
    Task<WatchProgressDto?> GetProgressAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates watch progress for a video. Applies first/last-5-minute reset logic.</summary>
    Task UpdateProgressAsync(Guid videoId, UpdateWatchProgressDto dto, CallerContext caller, CancellationToken cancellationToken = default);
}
