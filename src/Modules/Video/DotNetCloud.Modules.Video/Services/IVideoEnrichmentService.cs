using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Orchestrates TMDB movie metadata and poster art enrichment for videos.
/// </summary>
public interface IVideoEnrichmentService
{
    /// <summary>Whether TMDB enrichment is available (API key is configured).</summary>
    bool IsTmdbAvailable { get; }

    /// <summary>Enriches a single video with TMDB metadata and poster art.</summary>
    Task EnrichVideoAsync(Guid videoId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>Batch-enriches videos without external posters for a user.</summary>
    Task EnrichVideosWithoutPosterAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Batch-enriches all unenriched videos for a user.</summary>
    Task EnrichAllAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default);
}
