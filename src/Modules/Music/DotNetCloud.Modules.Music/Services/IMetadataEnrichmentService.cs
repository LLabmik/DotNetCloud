using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Orchestrates MusicBrainz lookups and applies enrichment data to music library entities.
/// </summary>
public interface IMetadataEnrichmentService
{
    /// <summary>
    /// Enriches an album with MusicBrainz metadata and Cover Art Archive cover art.
    /// Searches MB by album title + artist name, fetches release group, then cover art from CAA.
    /// </summary>
    /// <param name="albumId">Album to enrich.</param>
    /// <param name="caller">Caller context for authorization.</param>
    /// <param name="force">If true, re-enriches even if recently enriched (within 30-day window).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnrichAlbumAsync(Guid albumId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enriches an artist with MusicBrainz metadata (biography, external links).
    /// Searches MB by artist name, extracts annotation text and URL relations.
    /// </summary>
    /// <param name="artistId">Artist to enrich.</param>
    /// <param name="caller">Caller context for authorization.</param>
    /// <param name="force">If true, re-enriches even if recently enriched (within 30-day window).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnrichArtistAsync(Guid artistId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enriches a track with MusicBrainz recording ID.
    /// </summary>
    /// <param name="trackId">Track to enrich.</param>
    /// <param name="caller">Caller context for authorization.</param>
    /// <param name="force">If true, re-enriches even if recently enriched.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnrichTrackAsync(Guid trackId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-enriches all albums missing cover art for a user.
    /// </summary>
    /// <param name="ownerId">User whose albums to enrich.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnrichAlbumsWithoutArtAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-enriches all unenriched artists, albums, and tracks for a user.
    /// </summary>
    /// <param name="ownerId">User whose library to enrich.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnrichAllAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-fetches artist logos from TheAudioDB for all artists with an MBID but no logo.
    /// Uses concurrent requests (up to 10 at a time) since TheAudioDB allows ~20 req/s.
    /// </summary>
    /// <param name="ownerId">User whose artists to fetch logos for.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> EnrichArtistLogosAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches across all configured art sources (MusicBrainz, TheAudioDB) for album cover art.
    /// Returns merged results with thumbnails, sorted by relevance score.
    /// </summary>
    /// <param name="request">Search parameters (album title, artist name, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of art candidates from all sources, or empty if none found.</returns>
    Task<IReadOnlyList<FetchArtSearchResult>> SearchArtCandidatesAsync(FetchArtSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a selected art candidate to an album: downloads the cover art,
    /// saves it to content-addressed storage, and persists any source-specific IDs.
    /// </summary>
    /// <param name="albumId">Album to apply art to.</param>
    /// <param name="request">Which candidate to apply (source + source ID).</param>
    /// <param name="caller">Caller context for authorization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<ApplyArtResult> ApplyArtSelectionAsync(Guid albumId, FetchArtApplyRequest request, CallerContext caller, CancellationToken cancellationToken = default);
}
