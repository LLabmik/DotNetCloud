using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OrganizationsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationsController"/> class.
    /// </summary>
    public OrganizationsController(CoreDbContext db, UserManager<ApplicationUser> userManager, ILogger<OrganizationsController> logger)
    {
        _db = db;
        _userManager = userManager;
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
        var org = await _db.Organizations.AsTracking().FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);
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
        var org = await _db.Organizations.AsTracking().FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);
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

    /// <summary>
    /// Lists members of an organization.
    /// </summary>
    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> ListMembersAsync(Guid id, CancellationToken ct)
    {
        var orgExists = await _db.Organizations.AnyAsync(o => o.Id == id && !o.IsDeleted, ct);
        if (!orgExists)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Organization not found." } });

        var members = await _db.OrganizationMembers
            .Where(om => om.OrganizationId == id && om.IsActive)
            .Join(_userManager.Users, om => om.UserId, u => u.Id, (om, u) => new OrganizationMemberDto
            {
                UserId = u.Id,
                Email = u.Email!,
                DisplayName = u.DisplayName,
                JoinedAt = om.JoinedAt,
                IsActive = om.IsActive
            })
            .OrderBy(m => m.DisplayName)
            .ToListAsync(ct);

        return Ok(new { success = true, data = members });
    }

    /// <summary>
    /// Adds a user to an organization.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMemberAsync(Guid id, [FromBody] AddOrganizationMemberDto dto, CancellationToken ct)
    {
        var orgExists = await _db.Organizations.AnyAsync(o => o.Id == id && !o.IsDeleted, ct);
        if (!orgExists)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Organization not found." } });

        var user = await _userManager.FindByIdAsync(dto.UserId.ToString());
        if (user is null)
            return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found." } });

        var alreadyMember = await _db.OrganizationMembers
            .AnyAsync(om => om.OrganizationId == id && om.UserId == dto.UserId, ct);
        if (alreadyMember)
            return Conflict(new { success = false, error = new { code = "ALREADY_MEMBER", message = "User is already a member of this organization." } });

        var member = new OrganizationMember
        {
            OrganizationId = id,
            UserId = dto.UserId,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.OrganizationMembers.Add(member);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} added to organization {OrgId} by admin", dto.UserId, id);

        return Ok(new
        {
            success = true,
            data = new OrganizationMemberDto
            {
                UserId = user.Id,
                Email = user.Email!,
                DisplayName = user.DisplayName,
                JoinedAt = member.JoinedAt,
                IsActive = member.IsActive
            }
        });
    }

    /// <summary>
    /// Removes a user from an organization.
    /// </summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var member = await _db.OrganizationMembers
            .AsTracking()
            .FirstOrDefaultAsync(om => om.OrganizationId == id && om.UserId == userId, ct);
        if (member is null)
            return NotFound(new { success = false, error = new { code = "NOT_MEMBER", message = "User is not a member of this organization." } });

        _db.OrganizationMembers.Remove(member);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} removed from organization {OrgId} by admin", userId, id);

        return Ok(new { success = true, message = "Member removed." });
    }

    /// <summary>
    /// Lists users that are not members of the specified organization (for the add-member picker).
    /// </summary>
    [HttpGet("{id:guid}/non-members")]
    public async Task<IActionResult> ListNonMembersAsync(Guid id, [FromQuery] string? search, CancellationToken ct)
    {
        var orgExists = await _db.Organizations.AnyAsync(o => o.Id == id && !o.IsDeleted, ct);
        if (!orgExists)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Organization not found." } });

        var memberUserIds = _db.OrganizationMembers
            .Where(om => om.OrganizationId == id)
            .Select(om => om.UserId);

        var query = _userManager.Users
            .Where(u => u.IsActive && !memberUserIds.Contains(u.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => u.DisplayName.ToLower().Contains(term) || u.Email!.ToLower().Contains(term));
        }

        var users = await query
            .OrderBy(u => u.DisplayName)
            .Take(50)
            .Select(u => new OrganizationMemberDto
            {
                UserId = u.Id,
                Email = u.Email!,
                DisplayName = u.DisplayName,
                JoinedAt = default,
                IsActive = true
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = users });
    }
}
