using System.Security.Claims;
using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Admin endpoints for organization group management.
/// </summary>
[ApiController]
[Route("api/v1/core/admin/groups")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public sealed class GroupsController : ControllerBase
{
    private readonly IGroupDirectory _groupDirectory;
    private readonly IGroupManager _groupManager;
    private readonly CoreDbContext _dbContext;
    private readonly ILogger<GroupsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupsController"/> class.
    /// </summary>
    public GroupsController(
        IGroupDirectory groupDirectory,
        IGroupManager groupManager,
        CoreDbContext dbContext,
        ILogger<GroupsController> logger)
    {
        _groupDirectory = groupDirectory;
        _groupManager = groupManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Lists groups for an organization.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] Guid? organizationId, CancellationToken ct)
    {
        var groups = await _groupDirectory.GetGroupsForOrganizationAsync(organizationId ?? Guid.Empty, ct);
        var result = groups.Select(MapGroup).ToList();

        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Gets a single group by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var group = await _groupDirectory.GetGroupAsync(id, ct);
        if (group is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Group not found." } });

        return Ok(new { success = true, data = MapGroup(group) });
    }

    /// <summary>
    /// Creates a new group.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateGroupDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { success = false, error = new { code = "VALIDATION", message = "Name is required." } });

        try
        {
            var group = await _groupManager.CreateGroupAsync(dto.OrganizationId, dto.Name, dto.Description, ct);

            _logger.LogInformation("Group '{Name}' ({Id}) created by admin", group.Name, group.Id);

            return Created($"api/v1/core/admin/groups/{group.Id}", new
            {
                success = true,
                data = MapGroup(group),
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, error = new { code = "VALIDATION", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "CREATE_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Updates a group.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateGroupDto dto, CancellationToken ct)
    {
        try
        {
            var group = await _groupManager.UpdateGroupAsync(id, dto.Name, dto.Description, ct);
            if (group is null)
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Group not found." } });

            _logger.LogInformation("Group '{Name}' ({Id}) updated by admin", group.Name, group.Id);

            return Ok(new { success = true, data = MapGroup(group) });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, error = new { code = "VALIDATION", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "UPDATE_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Deletes a group.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var deleted = await _groupManager.DeleteGroupAsync(id, ct);
            if (!deleted)
                return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Group not found." } });

            _logger.LogInformation("Group {GroupId} deleted by admin", id);

            return Ok(new { success = true, message = "Group deleted." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "DELETE_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Lists members of a group.
    /// </summary>
    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> ListMembersAsync(Guid id, CancellationToken ct)
    {
        var group = await _groupDirectory.GetGroupAsync(id, ct);
        if (group is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Group not found." } });

        var members = await _groupDirectory.GetGroupMembersAsync(id, ct);
        var users = await LoadUsersByIdsAsync(members.Select(member => member.UserId), ct);
        var result = members.Select(member => MapMember(member, users.GetValueOrDefault(member.UserId))).ToList();

        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Adds a user to a group.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMemberAsync(Guid id, [FromBody] AddGroupMemberDto dto, CancellationToken ct)
    {
        var group = await _groupDirectory.GetGroupAsync(id, ct);
        if (group is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Group not found." } });

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == dto.UserId, ct);

        if (user is null)
            return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found." } });

        try
        {
            var added = await _groupManager.AddMemberAsync(id, dto.UserId, GetCurrentUserId(), ct);
            if (!added)
            {
                return Conflict(new
                {
                    success = false,
                    error = new { code = "ALREADY_MEMBER", message = "User is already a member of this group." },
                });
            }

            var membership = await _groupDirectory.GetGroupMemberAsync(id, dto.UserId, ct);
            if (membership is null)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = new { code = "MEMBERSHIP_LOOKUP_FAILED", message = "Group membership could not be loaded after creation." },
                });
            }

            _logger.LogInformation("User {UserId} added to group {GroupId} by admin", dto.UserId, id);

            return Ok(new { success = true, data = MapMember(membership, user) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "ADD_MEMBER_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Removes a user from a group.
    /// </summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var group = await _groupDirectory.GetGroupAsync(id, ct);
        if (group is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Group not found." } });

        var membership = await _groupDirectory.GetGroupMemberAsync(id, userId, ct);
        if (membership is null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "NOT_MEMBER", message = "User is not a member of this group." },
            });
        }

        try
        {
            await _groupManager.RemoveMemberAsync(id, userId, ct);

            _logger.LogInformation("User {UserId} removed from group {GroupId} by admin", userId, id);

            return Ok(new { success = true, message = "User removed from group." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "REMOVE_MEMBER_FAILED", message = ex.Message } });
        }
    }

    private async Task<Dictionary<Guid, ApplicationUser>> LoadUsersByIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var userIdList = userIds.Distinct().ToList();
        if (userIdList.Count == 0)
            return [];

        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => userIdList.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, ct);
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private static GroupDto MapGroup(GroupInfo group)
    {
        return new GroupDto
        {
            Id = group.Id,
            OrganizationId = group.OrganizationId,
            Name = group.Name,
            Description = group.Description,
            CreatedAt = group.CreatedAt,
            IsDeleted = false,
            DeletedAt = null,
            IsAllUsersGroup = group.IsAllUsersGroup,
            MemberCount = group.MemberCount,
        };
    }

    private static GroupMemberDto MapMember(GroupMemberInfo member, ApplicationUser? user)
    {
        return new GroupMemberDto
        {
            GroupId = member.GroupId,
            UserId = member.UserId,
            UserDisplayName = user?.DisplayName ?? string.Empty,
            UserEmail = user?.Email ?? string.Empty,
            AddedAt = member.AddedAt,
            AddedByUserId = member.AddedByUserId,
        };
    }
}