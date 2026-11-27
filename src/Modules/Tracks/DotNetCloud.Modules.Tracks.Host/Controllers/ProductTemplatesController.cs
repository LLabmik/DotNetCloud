using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for product templates.
/// </summary>
[ApiController]
public class ProductTemplatesController : TracksControllerBase
{
    private readonly ProductTemplateService _templateService;
    private readonly ItemTemplateService _itemTemplateService;
    private readonly TemplateSeedService _seedService;
    private readonly ILogger<ProductTemplatesController> _logger;

    public ProductTemplatesController(
        ProductTemplateService templateService,
        ItemTemplateService itemTemplateService,
        TemplateSeedService seedService,
        ILogger<ProductTemplatesController> logger)
    {
        _templateService = templateService;
        _itemTemplateService = itemTemplateService;
        _seedService = seedService;
        _logger = logger;
    }

    /// <summary>Lists all product templates, optionally filtered by category.</summary>
    [HttpGet("api/v1/product-templates")]
    public async Task<IActionResult> GetTemplatesAsync([FromQuery] string? category, CancellationToken ct)
    {
        // Ensure built-in templates are seeded
        await _seedService.EnsureSeededAsync(ct);

        var templates = await _templateService.GetTemplatesAsync(category, ct);
        return Ok(Envelope(templates));
    }

    /// <summary>Gets a single product template by ID.</summary>
    [HttpGet("api/v1/product-templates/{templateId:guid}")]
    public async Task<IActionResult> GetTemplateAsync(Guid templateId, CancellationToken ct)
    {
        try
        {
            var template = await _templateService.GetTemplateAsync(templateId, ct);
            return Ok(Envelope(template));
        }
        catch (System.InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>Creates a new product from a template.</summary>
    [HttpPost("api/v1/product-templates/{templateId:guid}/create-product")]
    public async Task<IActionResult> CreateProductFromTemplateAsync(
        Guid templateId,
        [FromBody] CreateProductFromTemplateDto dto,
        CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var product = await _templateService.CreateProductFromTemplateAsync(
                templateId, caller.UserId, dto, ct);
            return Created($"/api/v1/products/{product.Id}", Envelope(product));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Saves a product's configuration as a reusable template.</summary>
    [HttpPost("api/v1/products/{productId:guid}/save-as-template")]
    public async Task<IActionResult> SaveProductAsTemplateAsync(
        Guid productId,
        [FromBody] SaveProductAsTemplateDto dto,
        CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var template = await _templateService.SaveProductAsTemplateAsync(
                productId, caller.UserId, dto, ct);
            return Created($"/api/v1/product-templates/{template.Id}", Envelope(template));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Gets item templates for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/item-templates")]
    public async Task<IActionResult> GetItemTemplatesAsync(Guid productId, CancellationToken ct)
    {
        var templates = await _itemTemplateService.GetTemplatesByProductAsync(productId, ct);
        return Ok(Envelope(templates));
    }

    /// <summary>Creates a work item from an item template.</summary>
    [HttpPost("api/v1/item-templates/{templateId:guid}/create-item")]
    public async Task<IActionResult> CreateItemFromTemplateAsync(
        Guid templateId,
        [FromBody] CreateItemFromTemplateDto dto,
        CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

        if (!dto.SwimlaneId.HasValue)
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, "SwimlaneId is required when creating a work item from a template."));

        try
        {
            var item = await _itemTemplateService.CreateItemFromTemplateAsync(
                templateId, dto.SwimlaneId.Value, caller.UserId, dto, ct);
            return Created($"/api/v1/work-items/{item.Id}", Envelope(item));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }
}
