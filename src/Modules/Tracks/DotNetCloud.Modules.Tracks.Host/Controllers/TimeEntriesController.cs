using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for time tracking on cards.
/// </summary>
[Route("api/v1/cards/{cardId:guid}")]
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

    /// <summary>Lists time entries for a card.</summary>
    [HttpGet("time-entries")]
    public async Task<IActionResult> ListTimeEntriesAsync(Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var entries = await _timeTrackingService.GetTimeEntriesAsync(cardId, caller);
            return Ok(Envelope(entries));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    /// <summary>Creates a manual time entry.</summary>
    [HttpPost("time-entries")]
    public async Task<IActionResult> CreateTimeEntryAsync(Guid cardId, [FromBody] CreateTimeEntryDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var entry = await _timeTrackingService.CreateTimeEntryAsync(cardId, dto, caller);
            return Created($"/api/v1/cards/{cardId}/time-entries", Envelope(entry));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.CardNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.InvalidTimeEntry))
                return BadRequest(ErrorEnvelope(ErrorCodes.InvalidTimeEntry, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a time entry.</summary>
    [HttpDelete("time-entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteTimeEntryAsync(Guid cardId, Guid entryId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _timeTrackingService.DeleteTimeEntryAsync(entryId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.TimeEntryNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.TimeEntryNotFound, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Starts a timer on a card.</summary>
    [HttpPost("timer/start")]
    public async Task<IActionResult> StartTimerAsync(Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var entry = await _timeTrackingService.StartTimerAsync(cardId, caller);
            return Ok(Envelope(entry));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.CardNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.InvalidTimeEntry))
                return Conflict(ErrorEnvelope(ErrorCodes.InvalidTimeEntry, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Stops a running timer on a card.</summary>
    [HttpPost("timer/stop")]
    public async Task<IActionResult> StopTimerAsync(Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var entry = await _timeTrackingService.StopTimerAsync(cardId, caller);
            return Ok(Envelope(entry));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.CardNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.InvalidTimeEntry))
                return NotFound(ErrorEnvelope(ErrorCodes.InvalidTimeEntry, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}
