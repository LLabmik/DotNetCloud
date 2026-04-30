using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for saved custom views.
/// </summary>
[ApiController]
public class CustomViewsController : TracksControllerBase
{
    private readonly CustomViewService _viewService;
    private readonly ILogger<CustomViewsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomViewsController"/> class.
    /// </summary>
    public CustomViewsController(CustomViewService viewService, ILogger<CustomViewsController> logger)
    {
        _viewService = viewService;
        _logger = logger;
    }

    /// <summary>Lists custom views for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/views")]
    public async Task<IActionResult> ListViewsAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var views = await _viewService.GetViewsForProductAsync(productId, caller.UserId, ct);
            return Ok(Envelope(views.Select(v => ToDto(v))));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list custom views for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Creates a new custom view for a product.</summary>
    [HttpPost("api/v1/products/{productId:guid}/views")]
    public async Task<IActionResult> CreateViewAsync(Guid productId, [FromBody] CreateCustomViewDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var view = await _viewService.CreateViewAsync(
                productId, caller.UserId, dto.Name, dto.FilterJson ?? "{}",
                dto.SortJson ?? "{}", dto.GroupBy, dto.Layout ?? "Kanban",
                dto.IsShared, ct);

            return Created($"/api/v1/products/{productId}/views/{view.Id}", Envelope(ToDto(view)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create custom view for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Updates a custom view.</summary>
    [HttpPut("api/v1/products/{productId:guid}/views/{viewId:guid}")]
    public async Task<IActionResult> UpdateViewAsync(Guid productId, Guid viewId, [FromBody] UpdateCustomViewDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var view = await _viewService.UpdateViewAsync(
                viewId, caller.UserId, dto.Name, dto.FilterJson,
                dto.SortJson, dto.GroupBy, dto.Layout, dto.IsShared, ct);

            if (view is null) return NotFound(ErrorEnvelope(ErrorCodes.NotFound, "View not found."));
            return Ok(Envelope(ToDto(view)));
        }
        catch (System.InvalidOperationException ex) when (ex.Message.Contains("Not authorized"))
        {
            return StatusCode(403, ErrorEnvelope(ErrorCodes.Forbidden, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update custom view {ViewId}", viewId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Deletes a custom view.</summary>
    [HttpDelete("api/v1/products/{productId:guid}/views/{viewId:guid}")]
    public async Task<IActionResult> DeleteViewAsync(Guid productId, Guid viewId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var deleted = await _viewService.DeleteViewAsync(viewId, caller.UserId, ct);
            if (!deleted) return NotFound(ErrorEnvelope(ErrorCodes.NotFound, "View not found."));
            return Ok(Envelope(new { deleted = true }));
        }
        catch (System.InvalidOperationException ex) when (ex.Message.Contains("Not authorized"))
        {
            return StatusCode(403, ErrorEnvelope(ErrorCodes.Forbidden, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete custom view {ViewId}", viewId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    private static CustomViewDto ToDto(Models.CustomView v) => new()
    {
        Id = v.Id,
        ProductId = v.ProductId,
        UserId = v.UserId,
        Name = v.Name,
        FilterJson = v.FilterJson,
        SortJson = v.SortJson,
        GroupBy = v.GroupBy,
        Layout = v.Layout,
        IsShared = v.IsShared,
        CreatedAt = v.CreatedAt,
        UpdatedAt = v.UpdatedAt
    };
}

// ── Local DTOs ────────────────────────────────────────────────────────────

/// <summary>DTO for creating a custom view.</summary>
public sealed record CreateCustomViewDto
{
    public required string Name { get; init; }
    public string? FilterJson { get; init; }
    public string? SortJson { get; init; }
    public string? GroupBy { get; init; }
    public string? Layout { get; init; }
    public bool IsShared { get; init; }
}

/// <summary>DTO for updating a custom view.</summary>
public sealed record UpdateCustomViewDto
{
    public string? Name { get; init; }
    public string? FilterJson { get; init; }
    public string? SortJson { get; init; }
    public string? GroupBy { get; init; }
    public string? Layout { get; init; }
    public bool? IsShared { get; init; }
}
