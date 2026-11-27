using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for custom field definitions and work item field values.
/// </summary>
[ApiController]
public class CustomFieldsController : TracksControllerBase
{
    private readonly CustomFieldService _customFieldService;
    private readonly ILogger<CustomFieldsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFieldsController"/> class.
    /// </summary>
    public CustomFieldsController(CustomFieldService customFieldService, ILogger<CustomFieldsController> logger)
    {
        _customFieldService = customFieldService;
        _logger = logger;
    }

    // ─── Field Definitions ──────────────────────────────────────────────

    /// <summary>Lists all custom fields for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/custom-fields")]
    public async Task<IActionResult> GetFieldsAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var fields = await _customFieldService.GetFieldsAsync(productId, ct);
            return Ok(Envelope(fields));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing custom fields for product {ProductId}", productId);
            return StatusCode(500, ErrorEnvelope(ErrorCodes.InternalServerError, "Failed to list custom fields."));
        }
    }

    /// <summary>Creates a new custom field on a product.</summary>
    [HttpPost("api/v1/products/{productId:guid}/custom-fields")]
    public async Task<IActionResult> CreateFieldAsync(Guid productId, [FromBody] CreateCustomFieldDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var field = await _customFieldService.CreateFieldAsync(productId, dto, ct);
            return Created($"/api/v1/custom-fields/{field.Id}", Envelope(field));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Updates a custom field.</summary>
    [HttpPut("api/v1/custom-fields/{fieldId:guid}")]
    public async Task<IActionResult> UpdateFieldAsync(Guid fieldId, [FromBody] UpdateCustomFieldDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var field = await _customFieldService.UpdateFieldAsync(fieldId, dto, ct);
            return Ok(Envelope(field));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>Deletes a custom field.</summary>
    [HttpDelete("api/v1/custom-fields/{fieldId:guid}")]
    public async Task<IActionResult> DeleteFieldAsync(Guid fieldId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _customFieldService.DeleteFieldAsync(fieldId, ct);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>Reorders custom fields for a product.</summary>
    [HttpPut("api/v1/products/{productId:guid}/custom-fields/reorder")]
    public async Task<IActionResult> ReorderFieldsAsync(Guid productId, [FromBody] List<Guid> orderedIds, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _customFieldService.ReorderFieldsAsync(productId, orderedIds, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering custom fields for product {ProductId}", productId);
            return StatusCode(500, ErrorEnvelope(ErrorCodes.InternalServerError, "Failed to reorder custom fields."));
        }
    }

    // ─── Field Values ───────────────────────────────────────────────────

    /// <summary>Gets all custom field values for a work item.</summary>
    [HttpGet("api/v1/work-items/{workItemId:guid}/custom-field-values")]
    public async Task<IActionResult> GetFieldValuesAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var values = await _customFieldService.GetFieldValuesAsync(workItemId, ct);
            return Ok(Envelope(values));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing field values for work item {WorkItemId}", workItemId);
            return StatusCode(500, ErrorEnvelope(ErrorCodes.InternalServerError, "Failed to list field values."));
        }
    }

    /// <summary>Sets a custom field value on a work item.</summary>
    [HttpPut("api/v1/work-items/{workItemId:guid}/custom-field-values")]
    public async Task<IActionResult> SetFieldValueAsync(Guid workItemId, [FromBody] SetFieldValueDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var value = await _customFieldService.SetFieldValueAsync(workItemId, dto, ct);
            return Ok(Envelope(value));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Batch sets custom field values on a work item.</summary>
    [HttpPut("api/v1/work-items/{workItemId:guid}/custom-field-values/batch")]
    public async Task<IActionResult> BatchSetFieldValuesAsync(Guid workItemId, [FromBody] BatchSetFieldValuesDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _customFieldService.BatchSetFieldValuesAsync(workItemId, dto, ct);
            return Ok(Envelope(new { success = true }));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Clears a custom field value from a work item.</summary>
    [HttpDelete("api/v1/work-items/{workItemId:guid}/custom-field-values/{customFieldId:guid}")]
    public async Task<IActionResult> ClearFieldValueAsync(Guid workItemId, Guid customFieldId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _customFieldService.ClearFieldValueAsync(workItemId, customFieldId, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing field value for work item {WorkItemId}", workItemId);
            return StatusCode(500, ErrorEnvelope(ErrorCodes.InternalServerError, "Failed to clear field value."));
        }
    }
}
