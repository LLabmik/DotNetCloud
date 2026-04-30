using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for product and sprint analytics.
/// </summary>
[ApiController]
public class AnalyticsController : TracksControllerBase
{
    private readonly AnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsController"/> class.
    /// </summary>
    public AnalyticsController(AnalyticsService analyticsService, ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>Gets analytics for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/analytics")]
    public async Task<IActionResult> GetProductAnalyticsAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var analytics = await _analyticsService.GetProductAnalyticsAsync(productId, ct);
        return Ok(Envelope(analytics));
    }

    /// <summary>Gets velocity data (completed story points) for a product's completed sprints.</summary>
    [HttpGet("api/v1/products/{productId:guid}/velocity")]
    public async Task<IActionResult> GetVelocityDataAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var velocity = await _analyticsService.GetVelocityDataAsync(productId, ct);
        return Ok(Envelope(velocity));
    }

    /// <summary>Gets a sprint report with completion data.</summary>
    [HttpGet("api/v1/sprints/{sprintId:guid}/report")]
    public async Task<IActionResult> GetSprintReportAsync(Guid sprintId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var report = await _analyticsService.GetSprintReportAsync(sprintId, ct);
            return Ok(Envelope(report));
        }
        catch (System.InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
    }

    /// <summary>Gets burndown data for a sprint.</summary>
    [HttpGet("api/v1/sprints/{sprintId:guid}/burndown")]
    public async Task<IActionResult> GetBurndownDataAsync(Guid sprintId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var burndown = await _analyticsService.GetBurndownDataAsync(sprintId, ct);
            return Ok(Envelope(burndown));
        }
        catch (System.InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
    }

    /// <summary>Gets comprehensive dashboard data for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/dashboard")]
    public async Task<IActionResult> GetProductDashboardAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var dashboard = await _analyticsService.GetProductDashboardAsync(productId, ct);
            return Ok(Envelope(dashboard));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Gets roadmap data for a product (epics/features with dates and dependencies).</summary>
    [HttpGet("api/v1/products/{productId:guid}/roadmap")]
    public async Task<IActionResult> GetRoadmapDataAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var roadmap = await _analyticsService.GetRoadmapDataAsync(productId, ct);
            return Ok(Envelope(roadmap));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get roadmap for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Gets capacity data for a specific sprint.</summary>
    [HttpGet("api/v1/sprints/{sprintId:guid}/capacity")]
    public async Task<IActionResult> GetSprintCapacityAsync(Guid sprintId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var capacity = await _analyticsService.GetSprintCapacityAsync(sprintId, ct);
            return Ok(Envelope(capacity));
        }
        catch (System.InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
    }

    /// <summary>Gets capacity data for all members in a product's active sprints.</summary>
    [HttpGet("api/v1/products/{productId:guid}/analytics/capacity")]
    public async Task<IActionResult> GetMemberCapacityAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var capacity = await _analyticsService.GetProductCapacityAsync(productId, ct);
            return Ok(Envelope(capacity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get capacity for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }
}
