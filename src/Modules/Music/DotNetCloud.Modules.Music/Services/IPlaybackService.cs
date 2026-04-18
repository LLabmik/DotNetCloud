using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Manages playback history, scrobbling, and starred items.
/// </summary>
public interface IPlaybackService
{
    /// <summary>Records a play event for a track.</summary>
    Task RecordPlayAsync(Guid trackId, int durationPlayedSeconds, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Scrobbles a track play to external services.</summary>
    Task ScrobbleAsync(Guid trackId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Toggles the starred state of an item.</summary>
    Task ToggleStarAsync(Guid itemId, StarredItemType itemType, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Checks whether an item is starred.</summary>
    Task<bool> IsStarredAsync(Guid userId, Guid itemId, StarredItemType itemType, CancellationToken cancellationToken = default);
}
