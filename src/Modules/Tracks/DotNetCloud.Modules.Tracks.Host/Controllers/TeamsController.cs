using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for Tracks team management.
/// </summary>
[ApiController]
public class TeamsController : TracksControllerBase
{
    private readonly TracksDbContext _db;
    private readonly ILogger<TeamsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamsController"/> class.
    /// </summary>
    public TeamsController(TracksDbContext db, ILogger<TeamsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Lists all teams that the current user is a member of.</summary>
    [HttpGet("api/v1/teams")]
    public async Task<IActionResult> ListTeamsAsync(CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

        var teams = await _db.TeamRoles
            .Where(tr => tr.UserId == caller.UserId)
            .GroupBy(tr => tr.TeamId)
            .Select(g => new
            {
                TeamId = g.Key,
                MemberCount = g.Count(),
                CreatedAt = g.Min(tr => tr.AssignedAt)
            })
            .Join(_db.Teams,
                g => g.TeamId,
                t => t.Id,
                (g, t) => new TracksTeamDto
                {
                    Id = t.Id,
                    TeamId = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    MemberCount = g.MemberCount,
                    CreatedAt = t.CreatedAt
                })
            .ToListAsync(ct);

        return Ok(Envelope(teams));
    }

    /// <summary>Gets a single team by ID.</summary>
    [HttpGet("api/v1/teams/{teamId:guid}")]
    public async Task<IActionResult> GetTeamAsync(Guid teamId, CancellationToken ct)
    {
        var team = await _db.Teams
            .Where(t => t.Id == teamId)
            .Select(t => new TracksTeamDto
            {
                Id = t.Id,
                TeamId = t.Id,
                Name = t.Name,
                Description = t.Description,
                MemberCount = t.TeamRoles.Count,
                CreatedAt = t.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (team is null)
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, "Team not found."));

        return Ok(Envelope(team));
    }

    /// <summary>Creates a new team. The creator becomes the Owner.</summary>
    [HttpPost("api/v1/teams")]
    public async Task<IActionResult> CreateTeamAsync([FromBody] CreateTracksTeamDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, "Team name is required."));
        }

        var teamId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var team = new Team
        {
            Id = teamId,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            CreatedAt = now,
            CreatedByUserId = caller.UserId
        };

        var teamRole = new TeamRole
        {
            TeamId = teamId,
            UserId = caller.UserId,
            Role = TracksTeamMemberRole.Owner,
            AssignedAt = now
        };

        _db.Teams.Add(team);
        _db.TeamRoles.Add(teamRole);
        await _db.SaveChangesAsync(ct);

        var result = new TracksTeamDto
        {
            Id = teamId,
            TeamId = teamId,
            Name = team.Name,
            Description = team.Description,
            MemberCount = 1,
            CreatedAt = now
        };

        return Created($"/api/v1/teams/{teamId}", Envelope(result));
    }

    /// <summary>Updates a team's name and/or description.</summary>
    [HttpPut("api/v1/teams/{teamId:guid}")]
    public async Task<IActionResult> UpdateTeamAsync(Guid teamId, [FromBody] UpdateTracksTeamDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

        var team = await _db.Teams.FindAsync(new object[] { teamId }, ct);
        if (team is null)
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, "Team not found."));

        if (!await IsTeamOwnerOrManagerAsync(teamId, caller.UserId, ct))
            return Forbid();

        if (dto.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, "Team name cannot be empty."));
            team.Name = dto.Name.Trim();
        }

        if (dto.Description is not null)
            team.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        await _db.SaveChangesAsync(ct);

        var result = new TracksTeamDto
        {
            Id = team.Id,
            TeamId = team.Id,
            Name = team.Name,
            Description = team.Description,
            MemberCount = await _db.TeamRoles.CountAsync(tr => tr.TeamId == teamId, ct),
            CreatedAt = team.CreatedAt
        };

        return Ok(Envelope(result));
    }

    /// <summary>Deletes a team and all its member roles.</summary>
    [HttpDelete("api/v1/teams/{teamId:guid}")]
    public async Task<IActionResult> DeleteTeamAsync(Guid teamId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

        var team = await _db.Teams.FindAsync(new object[] { teamId }, ct);
        if (team is null)
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, "Team not found."));

        if (!await IsTeamOwnerOrManagerAsync(teamId, caller.UserId, ct))
            return Forbid();

        _db.Teams.Remove(team);
        await _db.SaveChangesAsync(ct);

        return Ok(Envelope(new { deleted = true }));
    }

    /// <summary>Gets members of a team.</summary>
    [HttpGet("api/v1/teams/{teamId:guid}/members")]
    public async Task<IActionResult> GetTeamMembersAsync(Guid teamId, CancellationToken ct)
    {
        var members = await _db.TeamRoles
            .Where(tr => tr.TeamId == teamId)
            .OrderBy(tr => tr.AssignedAt)
            .Select(tr => new TracksTeamMemberDto
            {
                UserId = tr.UserId,
                Role = tr.Role,
                AssignedAt = tr.AssignedAt
            })
            .ToListAsync(ct);

        return Ok(Envelope(members));
    }

    /// <summary>Adds a member to a team. Caller must be Owner or Manager.</summary>
    [HttpPost("api/v1/teams/{teamId:guid}/members")]
    public async Task<IActionResult> AddMemberAsync(Guid teamId, [FromBody] AddTracksTeamMemberRequest request, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

        if (!await IsTeamOwnerOrManagerAsync(teamId, caller.UserId, ct))
            return Forbid();

        var existing = await _db.TeamRoles
            .FirstOrDefaultAsync(tr => tr.TeamId == teamId && tr.UserId == request.UserId, ct);

        if (existing is not null)
        {
            return Conflict(ErrorEnvelope(ErrorCodes.TracksAlreadyTeamMember, "User is already a member of this team."));
        }

        var teamRole = new TeamRole
        {
            TeamId = teamId,
            UserId = request.UserId,
            Role = request.Role,
            AssignedAt = DateTime.UtcNow
        };

        _db.TeamRoles.Add(teamRole);
        await _db.SaveChangesAsync(ct);

        var result = new TracksTeamMemberDto
        {
            UserId = request.UserId,
            Role = request.Role,
            AssignedAt = teamRole.AssignedAt
        };

        return Created($"/api/v1/teams/{teamId}/members", Envelope(result));
    }

    /// <summary>Removes a member from a team. Caller must be Owner or Manager.</summary>
    [HttpDelete("api/v1/teams/{teamId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

        if (!await IsTeamOwnerOrManagerAsync(teamId, caller.UserId, ct))
            return Forbid();

        var teamRole = await _db.TeamRoles
            .FirstOrDefaultAsync(tr => tr.TeamId == teamId && tr.UserId == userId, ct);

        if (teamRole is null)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.TracksNotTeamMember, "Member not found in this team."));
        }

        _db.TeamRoles.Remove(teamRole);
        await _db.SaveChangesAsync(ct);

        return Ok(Envelope(new { removed = true }));
    }

    /// <summary>Updates a member's role. Caller must be Owner or Manager.</summary>
    [HttpPut("api/v1/teams/{teamId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRoleAsync(Guid teamId, Guid userId, [FromBody] UpdateTeamMemberRoleRequest request, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

        if (!await IsTeamOwnerOrManagerAsync(teamId, caller.UserId, ct))
            return Forbid();

        var teamRole = await _db.TeamRoles
            .FirstOrDefaultAsync(tr => tr.TeamId == teamId && tr.UserId == userId, ct);

        if (teamRole is null)
            return NotFound(ErrorEnvelope(ErrorCodes.TracksNotTeamMember, "Member not found in this team."));

        teamRole.Role = request.Role;
        await _db.SaveChangesAsync(ct);

        var result = new TracksTeamMemberDto
        {
            UserId = userId,
            Role = teamRole.Role,
            AssignedAt = teamRole.AssignedAt
        };

        return Ok(Envelope(result));
    }

    private async Task<bool> IsTeamOwnerOrManagerAsync(Guid teamId, Guid userId, CancellationToken ct)
    {
        var role = await _db.TeamRoles
            .Where(tr => tr.TeamId == teamId && tr.UserId == userId)
            .Select(tr => tr.Role)
            .FirstOrDefaultAsync(ct);

        return role is TracksTeamMemberRole.Owner or TracksTeamMemberRole.Manager;
    }
}

/// <summary>Request body for adding a team member.</summary>
public sealed record AddTracksTeamMemberRequest
{
    /// <summary>The user ID to add.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The role to assign.</summary>
    public required TracksTeamMemberRole Role { get; init; }
}

/// <summary>Request body for updating a member's role.</summary>
public sealed record UpdateTeamMemberRoleRequest
{
    /// <summary>The new role to assign.</summary>
    public required TracksTeamMemberRole Role { get; init; }
}
