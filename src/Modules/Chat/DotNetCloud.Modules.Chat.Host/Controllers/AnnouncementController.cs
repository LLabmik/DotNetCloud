using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Chat.Host.Controllers;

/// <summary>
/// REST API controller for organization-wide announcements.
/// </summary>
[ApiController]
[Route("api/v1/announcements")]
public class AnnouncementController : ControllerBase
{
    private readonly IAnnouncementService _announcementService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnnouncementController"/> class.
    /// </summary>
    public AnnouncementController(IAnnouncementService announcementService)
    {
        _announcementService = announcementService;
    }

    private static CallerContext ToCaller(Guid userId)
        => new(userId, ["user"], CallerType.User);

    /// <summary>Creates a new announcement (admin only).</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateAnnouncementDto dto, [FromQuery] Guid userId)
    {
        try
        {
            var result = await _announcementService.CreateAsync(dto, ToCaller(userId));
            return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, new { success = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
        }
    }

    /// <summary>Lists all announcements.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] Guid userId)
    {
        var result = await _announcementService.ListAsync(ToCaller(userId));
        return Ok(new { success = true, data = result });
    }

    /// <summary>Gets a single announcement by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, [FromQuery] Guid userId)
    {
        var result = await _announcementService.GetAsync(id, ToCaller(userId));
        if (result is null)
            return NotFound(new { success = false, error = new { code = "ANNOUNCEMENT_NOT_FOUND", message = "Announcement not found." } });

        return Ok(new { success = true, data = result });
    }

    /// <summary>Updates an announcement (admin only).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateAnnouncementDto dto, [FromQuery] Guid userId)
    {
        try
        {
            await _announcementService.UpdateAsync(id, dto, ToCaller(userId));
            return Ok(new { success = true, data = new { updated = true } });
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { success = false, error = new { code = "ANNOUNCEMENT_NOT_FOUND", message = "Announcement not found." } });
        }
    }

    /// <summary>Deletes an announcement (admin only).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            await _announcementService.DeleteAsync(id, ToCaller(userId));
            return Ok(new { success = true, data = new { deleted = true } });
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { success = false, error = new { code = "ANNOUNCEMENT_NOT_FOUND", message = "Announcement not found." } });
        }
    }

    /// <summary>Acknowledges an announcement.</summary>
    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAsync(Guid id, [FromQuery] Guid userId)
    {
        await _announcementService.AcknowledgeAsync(id, ToCaller(userId));
        return Ok(new { success = true, data = new { acknowledged = true } });
    }

    /// <summary>Lists who has acknowledged an announcement.</summary>
    [HttpGet("{id:guid}/acknowledgements")]
    public async Task<IActionResult> GetAcknowledgementsAsync(Guid id, [FromQuery] Guid userId)
    {
        var result = await _announcementService.GetAcknowledgementsAsync(id, ToCaller(userId));
        return Ok(new { success = true, data = result });
    }
}
