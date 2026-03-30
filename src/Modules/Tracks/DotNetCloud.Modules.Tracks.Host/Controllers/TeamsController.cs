using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for team management and membership in the Tracks module.
/// Teams are Core entities; this controller manages Tracks-specific role assignments and board linkage.
/// </summary>
[Route("api/v1/teams")]
public class TeamsController : TracksControllerBase
{
    private readonly TeamService _teamService;
    private readonly IUserDirectory _userDirectory;
    private readonly ILogger<TeamsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamsController"/> class.
    /// </summary>
    public TeamsController(TeamService teamService, IUserDirectory userDirectory, ILogger<TeamsController> logger)
    {
        _teamService = teamService;
        _userDirectory = userDirectory;
        _logger = logger;
    }

    // ─── Team CRUD ────────────────────────────────────────────────────

    /// <summary>Lists all teams for the authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> ListTeamsAsync()
    {
        var caller = GetAuthenticatedCaller();
        var teams = await _teamService.ListTeamsAsync(caller);
        return Ok(Envelope(teams));
    }

    /// <summary>Gets a team by ID.</summary>
    [HttpGet("{teamId:guid}")]
    public async Task<IActionResult> GetTeamAsync(Guid teamId)
    {
        var caller = GetAuthenticatedCaller();
        var team = await _teamService.GetTeamAsync(teamId, caller);
        return team is null
            ? NotFound(ErrorEnvelope(ErrorCodes.TracksTeamNotFound, "Team not found."))
            : Ok(Envelope(team));
    }

    /// <summary>Creates a new team.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateTeamAsync([FromBody] CreateTracksTeamDto dto)
    {
        var caller = GetAuthenticatedCaller();
        var team = await _teamService.CreateTeamAsync(dto, caller);
        return Created($"/api/v1/teams/{team.Id}", Envelope(team));
    }

    /// <summary>Updates a team.</summary>
    [HttpPut("{teamId:guid}")]
    public async Task<IActionResult> UpdateTeamAsync(Guid teamId, [FromBody] UpdateTracksTeamDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var team = await _teamService.UpdateTeamAsync(teamId, dto, caller);
            return Ok(Envelope(team));
        }
        catch (ValidationException ex)
        {
            return IsTeamNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.TracksTeamNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a team. Fails if the team has boards, unless cascade=true.</summary>
    [HttpDelete("{teamId:guid}")]
    public async Task<IActionResult> DeleteTeamAsync(Guid teamId, [FromQuery] bool cascade = false)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _teamService.DeleteTeamAsync(teamId, cascade, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            if (IsTeamNotFound(ex))
                return NotFound(ErrorEnvelope(ErrorCodes.TracksTeamNotFound, ex.Message));

            if (ex.Errors.ContainsKey(ErrorCodes.TracksTeamHasBoards))
                return Conflict(ErrorEnvelope(ErrorCodes.TracksTeamHasBoards, ex.Message));

            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Team Members ─────────────────────────────────────────────────

    /// <summary>Lists members of a team.</summary>
    [HttpGet("{teamId:guid}/members")]
    public async Task<IActionResult> ListMembersAsync(Guid teamId)
    {
        var caller = GetAuthenticatedCaller();
        var team = await _teamService.GetTeamAsync(teamId, caller);
        if (team is null)
            return NotFound(ErrorEnvelope(ErrorCodes.TracksTeamNotFound, "Team not found."));

        return Ok(Envelope(team.Members));
    }

    /// <summary>Adds a member to a team.</summary>
    [HttpPost("{teamId:guid}/members")]
    public async Task<IActionResult> AddMemberAsync(
        Guid teamId,
        [FromBody] AddTracksTeamMemberRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var member = await _teamService.AddMemberAsync(teamId, request.UserId, request.Role, caller);
            return Created($"/api/v1/teams/{teamId}/members", Envelope(member));
        }
        catch (ValidationException ex)
        {
            return IsTeamNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.TracksTeamNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Removes a member from a team.</summary>
    [HttpDelete("{teamId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(Guid teamId, Guid userId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _teamService.RemoveMemberAsync(teamId, userId, caller);
            return Ok(Envelope(new { removed = true }));
        }
        catch (ValidationException ex)
        {
            return IsTeamNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.TracksTeamNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates a member's role on a team.</summary>
    [HttpPut("{teamId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRoleAsync(
        Guid teamId,
        Guid userId,
        [FromBody] UpdateTracksTeamMemberRoleRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _teamService.UpdateMemberRoleAsync(teamId, userId, request.Role, caller);
            return Ok(Envelope(new { updated = true }));
        }
        catch (ValidationException ex)
        {
            return IsTeamNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.TracksTeamNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Team Boards ──────────────────────────────────────────────────

    /// <summary>Lists boards belonging to a team.</summary>
    [HttpGet("{teamId:guid}/boards")]
    public async Task<IActionResult> ListTeamBoardsAsync(
        Guid teamId,
        [FromQuery] bool includeArchived = false)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var boards = await _teamService.ListTeamBoardsAsync(teamId, caller, includeArchived);
            return Ok(Envelope(boards));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.TracksTeamNotFound, ex.Message));
        }
    }

    private static bool IsTeamNotFound(ValidationException ex)
    {
        return ex.Errors.ContainsKey(ErrorCodes.TracksTeamNotFound)
            || ex.Errors.ContainsKey(ErrorCodes.TracksNotTeamMember);
    }

    // ─── User Search (for member picker) ──────────────────────────────

    /// <summary>Searches users by display name or email for the add-member picker.</summary>
    [HttpGet("users/search")]
    public async Task<IActionResult> SearchUsersAsync([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(Envelope(Array.Empty<object>()));

        var results = await _userDirectory.SearchUsersAsync(q.Trim(), 20);
        return Ok(Envelope(results));
    }
}

// ─── Request DTOs (Controller-level, not shared) ──────────────────────────

/// <summary>Request body for adding a team member.</summary>
public sealed record AddTracksTeamMemberRequest
{
    /// <summary>The user ID to add.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The role to assign.</summary>
    public required TracksTeamMemberRole Role { get; init; }
}

/// <summary>Request body for updating a member's role.</summary>
public sealed record UpdateTracksTeamMemberRoleRequest
{
    /// <summary>The new role.</summary>
    public required TracksTeamMemberRole Role { get; init; }
}
