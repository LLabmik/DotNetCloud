using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Admin endpoints for organization management.
/// </summary>
[ApiController]
[Route("api/v1/core/admin/organizations")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public class OrganizationsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ILogger<OrganizationsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationsController"/> class.
    /// </summary>
    public OrganizationsController(CoreDbContext db, ILogger<OrganizationsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Lists all organizations.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var orgs = await _db.Organizations
            .Where(o => !o.IsDeleted)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new OrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                CreatedAt = o.CreatedAt,
                IsDeleted = o.IsDeleted,
                TeamCount = o.Teams.Count(t => !t.IsDeleted),
                MemberCount = o.Members.Count
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = orgs });
    }

    /// <summary>
    /// Gets a single organization by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var org = await _db.Organizations
            .Where(o => o.Id == id && !o.IsDeleted)
            .Select(o => new OrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                CreatedAt = o.CreatedAt,
                IsDeleted = o.IsDeleted,
                TeamCount = o.Teams.Count(t => !t.IsDeleted),
                MemberCount = o.Members.Count
            })
            .FirstOrDefaultAsync(ct);

        if (org is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Organization not found." } });

        return Ok(new { success = true, data = org });
    }

    /// <summary>
    /// Creates a new organization.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateOrganizationDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { success = false, error = new { code = "VALIDATION", message = "Name is required." } });

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Organization '{Name}' ({Id}) created by admin", org.Name, org.Id);

        return Created($"api/v1/core/admin/organizations/{org.Id}", new
        {
            success = true,
            data = new OrganizationDto
            {
                Id = org.Id,
                Name = org.Name,
                Description = org.Description,
                CreatedAt = org.CreatedAt
            }
        });
    }

    /// <summary>
    /// Updates an organization.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateOrganizationDto dto, CancellationToken ct)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);
        if (org is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Organization not found." } });

        if (!string.IsNullOrWhiteSpace(dto.Name))
            org.Name = dto.Name.Trim();
        if (dto.Description is not null)
            org.Description = dto.Description.Trim();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Organization '{Name}' ({Id}) updated by admin", org.Name, org.Id);

        return Ok(new
        {
            success = true,
            data = new OrganizationDto
            {
                Id = org.Id,
                Name = org.Name,
                Description = org.Description,
                CreatedAt = org.CreatedAt
            }
        });
    }

    /// <summary>
    /// Soft-deletes an organization.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);
        if (org is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Organization not found." } });

        // Prevent deleting the last organization
        var orgCount = await _db.Organizations.CountAsync(o => !o.IsDeleted, ct);
        if (orgCount <= 1)
            return BadRequest(new { success = false, error = new { code = "LAST_ORG", message = "Cannot delete the last organization." } });

        org.IsDeleted = true;
        org.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Organization '{Name}' ({Id}) deleted by admin", org.Name, org.Id);

        return Ok(new { success = true, message = "Organization deleted." });
    }
}
