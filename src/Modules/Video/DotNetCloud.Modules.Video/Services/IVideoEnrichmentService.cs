using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Orchestrates TMDB movie metadata and poster art enrichment for videos and series.
/// </summary>
public interface IVideoEnrichmentService
{
    /// <summary>Whether TMDB enrichment is available (API key is configured).</summary>
    bool IsTmdbAvailable { get; }

    /// <summary>Initializes runtime TMDB availability from database settings. Call once at startup.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Enriches a single video with TMDB metadata and poster art.</summary>
    Task EnrichVideoAsync(Guid videoId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default);

}
