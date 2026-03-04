using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for user storage quota management.
/// </summary>
[Route("api/v1/files/quota")]
public class QuotaController : FilesControllerBase
{
    private readonly IQuotaService _quotaService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuotaController"/> class.
    /// </summary>
    public QuotaController(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    /// <summary>
    /// Gets the current user's storage quota.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> GetCurrentAsync([FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var quota = await _quotaService.GetQuotaAsync(userId, ToCaller(userId));
        return Ok(Envelope(quota));
    });

    /// <summary>
    /// Gets a specific user's storage quota (admin).
    /// </summary>
    [HttpGet("{targetUserId:guid}")]
    public Task<IActionResult> GetAsync(Guid targetUserId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var quota = await _quotaService.GetQuotaAsync(targetUserId, ToCaller(userId));
        return Ok(Envelope(quota));
    });

    /// <summary>
    /// Sets a user's storage quota (admin).
    /// </summary>
    [HttpPut("{targetUserId:guid}")]
    public Task<IActionResult> SetAsync(Guid targetUserId, [FromBody] SetQuotaDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var quota = await _quotaService.SetQuotaAsync(targetUserId, dto.MaxBytes, ToCaller(userId));
        return Ok(Envelope(quota));
    });

    /// <summary>
    /// Forces recalculation of a user's used storage (admin).
    /// </summary>
    [HttpPost("{targetUserId:guid}/recalculate")]
    public Task<IActionResult> RecalculateAsync(Guid targetUserId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        await _quotaService.RecalculateAsync(targetUserId);
        var quota = await _quotaService.GetQuotaAsync(targetUserId, ToCaller(userId));
        return Ok(Envelope(quota));
    });
}
