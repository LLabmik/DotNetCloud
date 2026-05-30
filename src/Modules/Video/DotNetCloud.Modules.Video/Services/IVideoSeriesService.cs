using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Manages video series — TV series (with seasons/episodes) and movie franchises.
/// </summary>
public interface IVideoSeriesService
{
    // ─── Series CRUD ─────────────────────────────────────────────────

    /// <summary>Creates a new video series.</summary>
    Task<VideoSeriesDto> CreateSeriesAsync(CreateVideoSeriesDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a series by ID.</summary>
    Task<VideoSeriesDto?> GetSeriesAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all series for the caller.</summary>
    Task<IReadOnlyList<VideoSeriesDto>> ListSeriesAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates a series.</summary>
    Task<VideoSeriesDto> UpdateSeriesAsync(Guid seriesId, UpdateVideoSeriesDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes a series (soft delete).</summary>
    Task DeleteSeriesAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a series by name for the caller, or creates one if it doesn't exist.
    /// </summary>
    Task<VideoSeriesDto> FindOrCreateByNameAsync(string name, string type, CallerContext caller, CancellationToken cancellationToken = default);

    // ─── Franchise Items (Movie Franchises) ──────────────────────────

    /// <summary>Adds a video to a movie franchise series.</summary>
    Task<VideoSeriesItemDto> AddVideoToSeriesAsync(Guid seriesId, Guid videoId, int? sortOrder, string? episodeTitle, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a video from a series.</summary>
    Task RemoveVideoFromSeriesAsync(Guid seriesId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Reorders a video item within a series.</summary>
    Task ReorderSeriesItemAsync(Guid seriesId, Guid videoId, int newSortOrder, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all videos in a movie franchise series.</summary>
    Task<IReadOnlyList<VideoSeriesItemDto>> GetSeriesVideosAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default);

    // ─── Seasons (TV Series) ─────────────────────────────────────────

    /// <summary>Creates a new season within a TV series.</summary>
    Task<VideoSeasonDto> CreateSeasonAsync(CreateVideoSeasonDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a season by ID.</summary>
    Task<VideoSeasonDto?> GetSeasonAsync(Guid seasonId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all seasons for a TV series.</summary>
    Task<IReadOnlyList<VideoSeasonDto>> GetSeriesSeasonsAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates a season.</summary>
    Task<VideoSeasonDto> UpdateSeasonAsync(Guid seasonId, UpdateVideoSeasonDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes a season (soft delete).</summary>
    Task DeleteSeasonAsync(Guid seasonId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Finds or creates a season within a series by its season number.</summary>
    Task<VideoSeasonDto> FindOrCreateSeasonAsync(Guid seriesId, int seasonNumber, string? name, CallerContext caller, CancellationToken cancellationToken = default);

    // ─── Episodes (TV Series) ────────────────────────────────────────

    /// <summary>Adds a video as an episode to a season.</summary>
    Task<VideoEpisodeDto> AddEpisodeAsync(Guid seasonId, Guid videoId, int episodeNumber, string? title, string? overview, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes an episode from a season.</summary>
    Task RemoveEpisodeAsync(Guid seasonId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all episodes in a season.</summary>
    Task<IReadOnlyList<VideoEpisodeDto>> GetSeasonEpisodesAsync(Guid seasonId, CallerContext caller, CancellationToken cancellationToken = default);

    // ─── Auto-Detection ──────────────────────────────────────────────

    /// <summary>
    /// Scans the library for potential series groupings based on folder names and filename patterns.
    /// </summary>
    Task<IReadOnlyList<VideoSeriesDto>> DetectSeriesFromLibraryAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the poster thumbnail bytes for a series.
    /// </summary>
    Task<byte[]?> GetSeriesThumbnailAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enriches a series with TMDB metadata and poster art.
    /// For TV series: searches TMDB TV by name and fetches series poster.
    /// For movie franchises: searches TMDB collections by name and fetches poster.
    /// </summary>
    Task EnrichSeriesAsync(Guid seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the series that a video belongs to, if any. Checks both TV series episodes and movie franchise items.
    /// </summary>
    Task<VideoSeriesDto?> FindSeriesByVideoIdAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default);
}
