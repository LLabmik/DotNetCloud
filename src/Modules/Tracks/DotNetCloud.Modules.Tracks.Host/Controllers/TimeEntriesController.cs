using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for time tracking on work items.
/// </summary>
[ApiController]
public class TimeEntriesController : TracksControllerBase
{
    private readonly TimeTrackingService _timeTrackingService;
    private readonly ILogger<TimeEntriesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeEntriesController"/> class.
    /// </summary>
    public TimeEntriesController(TimeTrackingService timeTrackingService, ILogger<TimeEntriesController> logger)
    {
        _timeTrackingService = timeTrackingService;
        _logger = logger;
    }

    /// <summary>Lists time entries for a work item.</summary>
    [HttpGet("api/v1/workitems/{itemId:guid}/time-entries")]
    public async Task<IActionResult> GetTimeEntriesAsync(Guid itemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var entries = await _timeTrackingService.GetTimeEntriesByWorkItemAsync(itemId, ct);
        return Ok(Envelope(entries));
    }

    /// <summary>Creates a manual time entry for a work item.</summary>
    [HttpPost("api/v1/workitems/{itemId:guid}/time-entries")]
    public async Task<IActionResult> AddManualEntryAsync(Guid itemId, [FromBody] CreateTimeEntryDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var entry = await _timeTrackingService.AddManualEntryAsync(itemId, caller.UserId, dto, ct);
            return Created($"/api/v1/workitems/{itemId}/time-entries", Envelope(entry));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey("DurationMinutes")
                ? BadRequest(ErrorEnvelope(ErrorCodes.InvalidTimeEntry, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Deletes a time entry.</summary>
    [HttpDelete("api/v1/workitems/{itemId:guid}/time-entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntryAsync(Guid itemId, Guid entryId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _timeTrackingService.DeleteEntryAsync(entryId, ct);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.TimeEntryNotFound, ex.Message));
        }
    }

    /// <summary>Starts a timer on a work item.</summary>
    [HttpPost("api/v1/workitems/{itemId:guid}/timer/start")]
    public async Task<IActionResult> StartTimerAsync(Guid itemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var entry = await _timeTrackingService.StartTimerAsync(itemId, caller.UserId, ct);
            return Ok(Envelope(entry));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return Conflict(ErrorEnvelope(ErrorCodes.InvalidTimeEntry, ex.Message));
        }
    }

    /// <summary>Stops a running timer on a work item.</summary>
    [HttpPost("api/v1/workitems/{itemId:guid}/timer/stop")]
    public async Task<IActionResult> StopTimerAsync(Guid itemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var entry = await _timeTrackingService.StopTimerAsync(itemId, caller.UserId, ct);
            return Ok(Envelope(entry));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.TimeEntryNotFound, ex.Message));
        }
    }
}
