using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for product management and product-scoped resources (members, labels).
/// </summary>
[Route("api/v1")]
public class ProductsController : TracksControllerBase
{
    private readonly ProductService _productService;
    private readonly TracksDbContext _db;
    private readonly IUserDirectory _userDirectory;
    private readonly ILogger<ProductsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductsController"/> class.
    /// </summary>
    public ProductsController(ProductService productService, TracksDbContext db, IUserDirectory userDirectory, ILogger<ProductsController> logger)
    {
        _productService = productService;
        _db = db;
        _userDirectory = userDirectory;
        _logger = logger;
    }

    // ─── Product CRUD ──────────────────────────────────────────────────

    /// <summary>Lists all products in an organization.</summary>
    [HttpGet("organizations/{orgId:guid}/products")]
    public async Task<IActionResult> ListProductsByOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var products = await _productService.ListProductsByOrganizationAsync(orgId, ct);
            return Ok(Envelope(products));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list products for organization {OrgId}", orgId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Creates a new product in an organization. The authenticated user becomes the owner.</summary>
    [HttpPost("organizations/{orgId:guid}/products")]
    public async Task<IActionResult> CreateProductAsync(Guid orgId, [FromBody] CreateProductDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var product = await _productService.CreateProductAsync(orgId, caller.UserId, dto, ct);
            return Created($"/api/v1/products/{product.Id}", Envelope(product));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create product in organization {OrgId}", orgId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Gets a product by ID.</summary>
    [HttpGet("products/{productId:guid}")]
    public async Task<IActionResult> GetProductAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var product = await _productService.GetProductAsync(productId, ct);
            return Ok(Envelope(product));
        }
        catch (System.InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Updates a product.</summary>
    [HttpPut("products/{productId:guid}")]
    public async Task<IActionResult> UpdateProductAsync(Guid productId, [FromBody] UpdateProductDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var product = await _productService.UpdateProductAsync(productId, dto, ct);
            return Ok(Envelope(product));
        }
        catch (System.InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found"))
                return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
            if (ex.Message.Contains("modified by another user"))
                return Conflict(ErrorEnvelope(ErrorCodes.ConcurrencyConflict, ex.Message));
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Soft-deletes a product. Requires Admin or Owner role.</summary>
    [HttpDelete("products/{productId:guid}")]
    public async Task<IActionResult> DeleteProductAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var role = await _productService.GetUserProductRoleAsync(productId, caller.UserId, ct);
            if (role is not (ProductMemberRole.Admin or ProductMemberRole.Owner))
            {
                return Unauthorized(ErrorEnvelope(ErrorCodes.Forbidden, "Only admins and owners can delete a product."));
            }

            await _productService.DeleteProductAsync(productId, caller.UserId, ct);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (System.InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Lists soft-deleted products for an organization. Requires Admin or Owner for each.</summary>
    [HttpGet("organizations/{orgId:guid}/products/deleted")]
    public async Task<IActionResult> ListDeletedProductsAsync(Guid orgId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var deleted = await _productService.ListDeletedProductsAsync(orgId, ct);
            return Ok(Envelope(deleted));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list deleted products for org {OrgId}", orgId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Restores a soft-deleted product. Requires Admin or Owner role.</summary>
    [HttpPost("products/{productId:guid}/restore")]
    public async Task<IActionResult> RestoreProductAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var role = await _productService.GetUserProductRoleAsync(productId, caller.UserId, ct);
            if (role is not (ProductMemberRole.Admin or ProductMemberRole.Owner))
            {
                return Unauthorized(ErrorEnvelope(ErrorCodes.Forbidden, "Only admins and owners can restore a product."));
            }

            var product = await _productService.UndeleteProductAsync(productId, ct);
            return Ok(Envelope(product));
        }
        catch (System.InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Permanently deletes a product and all its data. Requires Admin or Owner role. Irreversible.</summary>
    [HttpDelete("products/{productId:guid}/permanent")]
    public async Task<IActionResult> PermanentDeleteProductAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var role = await _productService.GetUserProductRoleAsync(productId, caller.UserId, ct);
            if (role is not (ProductMemberRole.Admin or ProductMemberRole.Owner))
            {
                return Unauthorized(ErrorEnvelope(ErrorCodes.Forbidden, "Only admins and owners can permanently delete a product."));
            }

            await _productService.HardDeleteProductAsync(productId, ct);
            return Ok(Envelope(new { deleted = true, permanent = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to permanently delete product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    // ─── Product Members ───────────────────────────────────────────────

    /// <summary>Lists all members of a product.</summary>
    [HttpGet("products/{productId:guid}/members")]
    public async Task<IActionResult> ListMembersAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var productExists = await _db.Products.AnyAsync(p => p.Id == productId, ct);
            if (!productExists)
                return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, "Product not found."));

            var members = await _db.ProductMembers
                .Where(pm => pm.ProductId == productId)
                .Select(pm => new ProductMemberDto
                {
                    UserId = pm.UserId,
                    DisplayName = null,
                    Role = pm.Role,
                    JoinedAt = pm.JoinedAt
                })
                .ToListAsync(ct);

            // Resolve display names for all members
            var userIds = members.Select(m => m.UserId).Distinct().ToList();
            if (userIds.Count > 0)
            {
                var displayNames = await _userDirectory.GetDisplayNamesAsync(userIds, ct);
                for (var i = 0; i < members.Count; i++)
                {
                    if (displayNames.TryGetValue(members[i].UserId, out var name))
                    {
                        members[i] = members[i] with { DisplayName = name };
                    }
                }
            }

            return Ok(Envelope(members));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list members for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Adds a member to a product.</summary>
    [HttpPost("products/{productId:guid}/members")]
    public async Task<IActionResult> AddMemberAsync(Guid productId, [FromBody] AddProductMemberDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var member = await _productService.AddMemberAsync(productId, dto, ct);
            return Created($"/api/v1/products/{productId}/members", Envelope(member));
        }
        catch (System.InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found"))
                return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
            if (ex.Message.Contains("already a member"))
                return Conflict(ErrorEnvelope(ErrorCodes.Conflict, ex.Message));
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add member to product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Removes a member from a product.</summary>
    [HttpDelete("products/{productId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(Guid productId, Guid userId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _productService.RemoveMemberAsync(productId, userId, ct);
            return Ok(Envelope(new { removed = true }));
        }
        catch (System.InvalidOperationException ex)
        {
            if (ex.Message.Contains("not a member"))
                return NotFound(ErrorEnvelope(ErrorCodes.NotBoardMember, ex.Message));
            if (ex.Message.Contains("last Owner"))
                return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove member {UserId} from product {ProductId}", userId, productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Updates a member's role on a product.</summary>
    [HttpPut("products/{productId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRoleAsync(
        Guid productId, Guid userId, [FromBody] UpdateProductMemberRoleRequest request, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var member = await _productService.UpdateMemberRoleAsync(productId, userId, request.Role, ct);
            return Ok(Envelope(member));
        }
        catch (System.InvalidOperationException ex)
        {
            if (ex.Message.Contains("not a member"))
                return NotFound(ErrorEnvelope(ErrorCodes.NotBoardMember, ex.Message));
            if (ex.Message.Contains("last Owner"))
                return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update role for user {UserId} in product {ProductId}", userId, productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    // ─── Labels ────────────────────────────────────────────────────────

    /// <summary>Lists all labels for a product.</summary>
    [HttpGet("products/{productId:guid}/labels")]
    public async Task<IActionResult> ListLabelsAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var productExists = await _db.Products.AnyAsync(p => p.Id == productId, ct);
            if (!productExists)
                return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, "Product not found."));

            var labels = await _db.Labels
                .Where(l => l.ProductId == productId)
                .Select(l => new LabelDto
                {
                    Id = l.Id,
                    ProductId = l.ProductId,
                    Title = l.Title,
                    Color = l.Color,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync(ct);

            return Ok(Envelope(labels));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list labels for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Creates a new label on a product.</summary>
    [HttpPost("products/{productId:guid}/labels")]
    public async Task<IActionResult> CreateLabelAsync(Guid productId, [FromBody] CreateLabelDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var label = await _productService.CreateLabelAsync(productId, dto, ct);
            return Created($"/api/v1/products/{productId}/labels/{label.Id}", Envelope(label));
        }
        catch (System.InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create label for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Updates a label's title and/or color.</summary>
    [HttpPut("products/{productId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> UpdateLabelAsync(
        Guid productId, Guid labelId, [FromBody] UpdateLabelDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var label = await _db.Labels
                .FirstOrDefaultAsync(l => l.Id == labelId && l.ProductId == productId, ct);

            if (label is null)
                return NotFound(ErrorEnvelope(ErrorCodes.LabelNotFound, "Label not found."));

            if (dto.Title is not null)
                label.Title = dto.Title;
            if (dto.Color is not null)
                label.Color = dto.Color;

            await _db.SaveChangesAsync(ct);

            var result = new LabelDto
            {
                Id = label.Id,
                ProductId = label.ProductId,
                Title = label.Title,
                Color = label.Color,
                CreatedAt = label.CreatedAt
            };

            return Ok(Envelope(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update label {LabelId} in product {ProductId}", labelId, productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Deletes a label from a product.</summary>
    [HttpDelete("products/{productId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> DeleteLabelAsync(Guid productId, Guid labelId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _productService.DeleteLabelAsync(productId, labelId, ct);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (System.InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.LabelNotFound, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete label {LabelId} from product {ProductId}", labelId, productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }
}

// ─── Request DTOs (Controller-level, not shared) ──────────────────────────

/// <summary>Request body for updating a product member's role.</summary>
public sealed record UpdateProductMemberRoleRequest
{
    /// <summary>The new role to assign.</summary>
    public required ProductMemberRole Role { get; init; }
}
