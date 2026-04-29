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

    /// <summary>Lists all teams (grouped by TeamId) that the current user is a member of.</summary>
    [HttpGet("api/v1/teams")]
    public async Task<IActionResult> ListTeamsAsync(CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var teamGroups = await _db.TeamRoles
            .Where(tr => tr.UserId == caller.UserId)
            .GroupBy(tr => tr.TeamId)
            .Select(g => new
            {
                TeamId = g.Key,
                MemberCount = g.Count(),
                CreatedAt = g.Min(tr => tr.AssignedAt)
            })
            .ToListAsync(ct);

        var teams = teamGroups.Select(g => new TracksTeamDto
        {
            Id = g.TeamId,
            TeamId = g.TeamId,
            Name = "Team " + g.TeamId.ToString()[..8],
            MemberCount = g.MemberCount,
            CreatedAt = g.CreatedAt
        }).ToList();

        return Ok(Envelope(teams));
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

        var teamRole = new TeamRole
        {
            TeamId = teamId,
            UserId = caller.UserId,
            Role = TracksTeamMemberRole.Owner,
            AssignedAt = DateTime.UtcNow
        };

        _db.TeamRoles.Add(teamRole);
        await _db.SaveChangesAsync(ct);

        var result = new TracksTeamDto
        {
            Id = teamId,
            TeamId = teamId,
            Name = dto.Name,
            Description = dto.Description,
            MemberCount = 1,
            CreatedAt = DateTime.UtcNow
        };

        return Created($"/api/v1/teams/{teamId}", Envelope(result));
    }

    /// <summary>Gets members of a team.</summary>
    [HttpGet("api/v1/teams/{teamId:guid}/members")]
    public async Task<IActionResult> GetTeamMembersAsync(Guid teamId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

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

    /// <summary>Adds a member to a team.</summary>
    [HttpPost("api/v1/teams/{teamId:guid}/members")]
    public async Task<IActionResult> AddMemberAsync(Guid teamId, [FromBody] AddTracksTeamMemberRequest request, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

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

    /// <summary>Removes a member from a team.</summary>
    [HttpDelete("api/v1/teams/{teamId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

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
}

/// <summary>Request body for adding a team member.</summary>
public sealed record AddTracksTeamMemberRequest
{
    /// <summary>The user ID to add.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The role to assign.</summary>
    public required TracksTeamMemberRole Role { get; init; }
}
