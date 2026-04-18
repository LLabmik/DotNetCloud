using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Manages video watch progress and continue-watching.
/// </summary>
public interface IWatchProgressService
{
    /// <summary>Updates watch progress for a video.</summary>
    Task UpdateProgressAsync(Guid videoId, UpdateWatchProgressDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the current watch progress for a video.</summary>
    Task<WatchProgressDto?> GetProgressAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets videos the user is currently watching (not finished).</summary>
    Task<IReadOnlyList<WatchProgressDto>> GetContinueWatchingAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default);

    /// <summary>Records a video view.</summary>
    Task RecordViewAsync(Guid videoId, CallerContext caller, int durationWatchedSeconds = 0, CancellationToken cancellationToken = default);
}
