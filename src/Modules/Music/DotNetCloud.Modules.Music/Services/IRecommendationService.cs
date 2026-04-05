using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Provides music recommendations and discovery.
/// </summary>
public interface IRecommendationService
{
    /// <summary>Gets recently played tracks.</summary>
    Task<IReadOnlyList<TrackDto>> GetRecentlyPlayedAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default);

    /// <summary>Gets the most played tracks.</summary>
    Task<IReadOnlyList<TrackDto>> GetMostPlayedAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default);

    /// <summary>Gets tracks similar to the given track.</summary>
    Task<IReadOnlyList<TrackDto>> GetSimilarTracksAsync(Guid trackId, CallerContext caller, int count = 10, CancellationToken cancellationToken = default);

    /// <summary>Gets newly added tracks.</summary>
    Task<IReadOnlyList<TrackDto>> GetNewAdditionsAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default);

    /// <summary>Gets all distinct genre names in the user's library.</summary>
    Task<IReadOnlyList<string>> GetGenresAsync(Guid userId, CancellationToken cancellationToken = default);
}
