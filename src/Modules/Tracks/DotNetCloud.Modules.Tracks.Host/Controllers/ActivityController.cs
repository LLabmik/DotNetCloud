using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for Tracks activity feeds.
/// </summary>
[ApiController]
public class ActivityController : TracksControllerBase
{
    private readonly ActivityService _activityService;
    private readonly ILogger<ActivityController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityController"/> class.
    /// </summary>
    public ActivityController(ActivityService activityService, ILogger<ActivityController> logger)
    {
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>Lists recent activity for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/activity")]
    public async Task<IActionResult> GetProductActivityAsync(Guid productId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();
        var activities = await _activityService.GetActivitiesByProductAsync(productId, skip, take, ct);
        return Ok(Envelope(activities));
    }

    /// <summary>Lists recent activity for a work item.</summary>
    [HttpGet("api/v1/workitems/{workItemId:guid}/activity")]
    public async Task<IActionResult> GetWorkItemActivityAsync(Guid workItemId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();
        var activities = await _activityService.GetActivitiesByWorkItemAsync(workItemId, skip, take, ct);
        return Ok(Envelope(activities));
    }
}
