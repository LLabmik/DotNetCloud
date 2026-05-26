using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Video.Host.Controllers;

/// <summary>
/// REST API controller for video series management — TV series and movie franchises.
/// </summary>
[Route("api/v1/series")]
public class SeriesController : VideoControllerBase
{
    private readonly VideoSeriesService _seriesService;
    private readonly IVideoEnrichmentService _enrichmentService;
    private readonly ILogger<SeriesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesController"/> class.
    /// </summary>
    public SeriesController(
        VideoSeriesService seriesService,
        IVideoEnrichmentService enrichmentService,
        ILogger<SeriesController> logger)
    {
        _seriesService = seriesService;
        _enrichmentService = enrichmentService;
        _logger = logger;
    }

    // ─── Series CRUD ─────────────────────────────────────────────────

    /// <summary>Lists all series for the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> ListSeries()
    {
        var caller = GetAuthenticatedCaller();
        var series = await _seriesService.ListSeriesAsync(caller);
        return Ok(Envelope(series));
    }

    /// <summary>Gets a series by ID.</summary>
    [HttpGet("{seriesId:guid}")]
    public async Task<IActionResult> GetSeries(Guid seriesId)
    {
        var caller = GetAuthenticatedCaller();
        var series = await _seriesService.GetSeriesAsync(seriesId, caller);
        return series is null
            ? NotFound(ErrorEnvelope(ErrorCodes.VideoSeriesNotFound, "Series not found."))
            : Ok(Envelope(series));
    }

    /// <summary>Creates a new video series.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSeries([FromBody] CreateVideoSeriesDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var series = await _seriesService.CreateSeriesAsync(dto, caller);
            return Created($"/api/v1/series/{series.Id}", Envelope(series));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeriesAlreadyExists)
        {
            return Conflict(ErrorEnvelope(ErrorCodes.VideoSeriesAlreadyExists, ex.Message));
        }
    }

    /// <summary>Updates a series.</summary>
    [HttpPut("{seriesId:guid}")]
    public async Task<IActionResult> UpdateSeries(Guid seriesId, [FromBody] UpdateVideoSeriesDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var series = await _seriesService.UpdateSeriesAsync(seriesId, dto, caller);
            return Ok(Envelope(series));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeriesNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeriesNotFound, ex.Message));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeriesAlreadyExists)
        {
            return Conflict(ErrorEnvelope(ErrorCodes.VideoSeriesAlreadyExists, ex.Message));
        }
    }

    /// <summary>Deletes a series (soft delete).</summary>
    [HttpDelete("{seriesId:guid}")]
    public async Task<IActionResult> DeleteSeries(Guid seriesId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _seriesService.DeleteSeriesAsync(seriesId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeriesNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeriesNotFound, ex.Message));
        }
    }

    /// <summary>Triggers TMDB enrichment for a series.</summary>
    [HttpPost("{seriesId:guid}/enrich")]
    public async Task<IActionResult> EnrichSeries(Guid seriesId, [FromQuery] bool force = false)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _enrichmentService.EnrichSeriesAsync(seriesId, caller, force);
            var series = await _seriesService.GetSeriesAsync(seriesId, caller);
            return series is null
                ? NotFound(ErrorEnvelope(ErrorCodes.VideoSeriesNotFound, "Series not found."))
                : Ok(Envelope(series));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeriesNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeriesNotFound, ex.Message));
        }
    }

    // ─── Franchise Items ─────────────────────────────────────────────

    /// <summary>Gets all videos in a movie franchise series.</summary>
    [HttpGet("{seriesId:guid}/videos")]
    public async Task<IActionResult> GetSeriesVideos(Guid seriesId)
    {
        var caller = GetAuthenticatedCaller();
        var videos = await _seriesService.GetSeriesVideosAsync(seriesId, caller);
        return Ok(Envelope(videos));
    }

    /// <summary>Adds a video to a movie franchise series.</summary>
    [HttpPost("{seriesId:guid}/videos")]
    public async Task<IActionResult> AddVideoToSeries(Guid seriesId, [FromBody] AddVideoToSeriesDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var item = await _seriesService.AddVideoToSeriesAsync(seriesId, dto.VideoId, dto.SortOrder, dto.EpisodeTitle, caller);
            return Created($"/api/v1/series/{seriesId}/videos/{dto.VideoId}", Envelope(item));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeriesNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeriesNotFound, ex.Message));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, ex.Message));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoAlreadyInSeries)
        {
            return Conflict(ErrorEnvelope(ErrorCodes.VideoAlreadyInSeries, ex.Message));
        }
    }

    /// <summary>Removes a video from a franchise series.</summary>
    [HttpDelete("{seriesId:guid}/videos/{videoId:guid}")]
    public async Task<IActionResult> RemoveVideoFromSeries(Guid seriesId, Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _seriesService.RemoveVideoFromSeriesAsync(seriesId, videoId, caller);
            return Ok(Envelope(new { removed = true }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeriesNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeriesNotFound, ex.Message));
        }
    }

    /// <summary>Reorders a video within a franchise series.</summary>
    [HttpPut("{seriesId:guid}/videos/{videoId:guid}/reorder")]
    public async Task<IActionResult> ReorderSeriesItem(Guid seriesId, Guid videoId, [FromBody] ReorderRequestDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _seriesService.ReorderSeriesItemAsync(seriesId, videoId, dto.NewSortOrder, caller);
            return Ok(Envelope(new { reordered = true }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeriesNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeriesNotFound, ex.Message));
        }
    }

    // ─── Seasons ─────────────────────────────────────────────────────

    /// <summary>Lists all seasons in a TV series.</summary>
    [HttpGet("{seriesId:guid}/seasons")]
    public async Task<IActionResult> ListSeasons(Guid seriesId)
    {
        var caller = GetAuthenticatedCaller();
        var seasons = await _seriesService.GetSeriesSeasonsAsync(seriesId, caller);
        return Ok(Envelope(seasons));
    }

    /// <summary>Gets a season by ID.</summary>
    [HttpGet("{seriesId:guid}/seasons/{seasonId:guid}")]
    public async Task<IActionResult> GetSeason(Guid seriesId, Guid seasonId)
    {
        var caller = GetAuthenticatedCaller();
        var season = await _seriesService.GetSeasonAsync(seasonId, caller);
        return season is null
            ? NotFound(ErrorEnvelope(ErrorCodes.VideoSeasonNotFound, "Season not found."))
            : Ok(Envelope(season));
    }

    /// <summary>Creates a new season in a TV series.</summary>
    [HttpPost("{seriesId:guid}/seasons")]
    public async Task<IActionResult> CreateSeason(Guid seriesId, [FromBody] CreateVideoSeasonDto dto)
    {
        var caller = GetAuthenticatedCaller();
        // Ensure the series ID in the route matches the DTO
        var seasonDto = dto with { SeriesId = seriesId };
        try
        {
            var season = await _seriesService.CreateSeasonAsync(seasonDto, caller);
            return Created($"/api/v1/series/{seriesId}/seasons/{season.Id}", Envelope(season));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeriesNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeriesNotFound, ex.Message));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeasonNotFound)
        {
            return Conflict(ErrorEnvelope(ErrorCodes.VideoSeasonNotFound, ex.Message));
        }
    }

    /// <summary>Updates a season.</summary>
    [HttpPut("{seriesId:guid}/seasons/{seasonId:guid}")]
    public async Task<IActionResult> UpdateSeason(Guid seriesId, Guid seasonId, [FromBody] UpdateVideoSeasonDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var season = await _seriesService.UpdateSeasonAsync(seasonId, dto, caller);
            return Ok(Envelope(season));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeasonNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeasonNotFound, ex.Message));
        }
    }

    /// <summary>Deletes a season (soft delete).</summary>
    [HttpDelete("{seriesId:guid}/seasons/{seasonId:guid}")]
    public async Task<IActionResult> DeleteSeason(Guid seriesId, Guid seasonId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _seriesService.DeleteSeasonAsync(seasonId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeasonNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeasonNotFound, ex.Message));
        }
    }

    // ─── Episodes ────────────────────────────────────────────────────

    /// <summary>Lists all episodes in a season.</summary>
    [HttpGet("{seriesId:guid}/seasons/{seasonId:guid}/episodes")]
    public async Task<IActionResult> ListEpisodes(Guid seriesId, Guid seasonId)
    {
        var caller = GetAuthenticatedCaller();
        var episodes = await _seriesService.GetSeasonEpisodesAsync(seasonId, caller);
        return Ok(Envelope(episodes));
    }

    /// <summary>Adds a video as an episode to a season.</summary>
    [HttpPost("{seriesId:guid}/seasons/{seasonId:guid}/episodes")]
    public async Task<IActionResult> AddEpisode(Guid seriesId, Guid seasonId, [FromBody] AddEpisodeDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var episode = await _seriesService.AddEpisodeAsync(seasonId, dto.VideoId, dto.EpisodeNumber, dto.Title, dto.Overview, caller);
            return Created($"/api/v1/series/{seriesId}/seasons/{seasonId}/episodes/{dto.VideoId}", Envelope(episode));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeasonNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeasonNotFound, ex.Message));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, ex.Message));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoAlreadyInSeason)
        {
            return Conflict(ErrorEnvelope(ErrorCodes.VideoAlreadyInSeason, ex.Message));
        }
    }

    /// <summary>Removes an episode from a season.</summary>
    [HttpDelete("{seriesId:guid}/seasons/{seasonId:guid}/episodes/{videoId:guid}")]
    public async Task<IActionResult> RemoveEpisode(Guid seriesId, Guid seasonId, Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _seriesService.RemoveEpisodeAsync(seasonId, videoId, caller);
            return Ok(Envelope(new { removed = true }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoSeasonNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoSeasonNotFound, ex.Message));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoEpisodeNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoEpisodeNotFound, ex.Message));
        }
    }

    // ─── Thumbnail ──────────────────────────────────────────────────

    /// <summary>Gets the poster thumbnail for a series.</summary>
    [HttpGet("{seriesId:guid}/thumbnail")]
    public async Task<IActionResult> GetSeriesThumbnail(Guid seriesId)
    {
        var caller = GetAuthenticatedCaller();
        var bytes = await _seriesService.GetSeriesThumbnailAsync(seriesId, caller);
        if (bytes is null || bytes.Length == 0)
            return NotFound();

        Response.Headers.CacheControl = "private, max-age=3600";
        return File(bytes, "image/jpeg");
    }

    // ─── Auto-Detection ──────────────────────────────────────────────

    /// <summary>
    /// Scans the library for potential series groupings based on folder names and filename patterns.
    /// Returns existing or newly created series for detected groups.
    /// </summary>
    [HttpPost("detect")]
    public async Task<IActionResult> DetectSeries()
    {
        var caller = GetAuthenticatedCaller();
        var series = await _seriesService.DetectSeriesFromLibraryAsync(caller);
        return Ok(Envelope(series));
    }

}
