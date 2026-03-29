using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for board and team analytics.
/// </summary>
[Route("api/v1")]
public class AnalyticsController : TracksControllerBase
{
    private readonly AnalyticsService _analyticsService;
    private readonly SprintReportService _sprintReportService;
    private readonly ILogger<AnalyticsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsController"/> class.
    /// </summary>
    public AnalyticsController(AnalyticsService analyticsService, SprintReportService sprintReportService, ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _sprintReportService = sprintReportService;
        _logger = logger;
    }

    /// <summary>Gets analytics for a board.</summary>
    [HttpGet("boards/{boardId:guid}/analytics")]
    public async Task<IActionResult> GetBoardAnalyticsAsync(Guid boardId, [FromQuery] int daysBack = 30)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var analytics = await _analyticsService.GetBoardAnalyticsAsync(boardId, caller, daysBack);
            return Ok(Envelope(analytics));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets analytics for a team (aggregated across all team boards).</summary>
    [HttpGet("teams/{teamId:guid}/analytics")]
    public async Task<IActionResult> GetTeamAnalyticsAsync(Guid teamId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var analytics = await _analyticsService.GetTeamAnalyticsAsync(teamId, caller);
            return Ok(Envelope(analytics));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.TracksNotTeamMember)
                ? Forbid()
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets a sprint report with burndown and completion data.</summary>
    [HttpGet("sprints/{sprintId:guid}/report")]
    public async Task<IActionResult> GetSprintReportAsync(Guid sprintId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var report = await _sprintReportService.GetSprintReportAsync(sprintId, caller);
            return Ok(Envelope(report));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.SprintNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets velocity data for all completed sprints on a board.</summary>
    [HttpGet("boards/{boardId:guid}/velocity")]
    public async Task<IActionResult> GetBoardVelocityAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var velocity = await _sprintReportService.GetBoardVelocityAsync(boardId, caller);
            return Ok(Envelope(velocity));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}
