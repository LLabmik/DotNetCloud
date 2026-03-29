using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a user's Tracks-specific role within a Core team.
/// Core teams provide identity and membership; this entity adds module-level role semantics
/// (Owner, Manager, Member) that determine board access levels.
/// </summary>
public sealed class TeamRole
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The Core team ID this role applies to.</summary>
    public Guid CoreTeamId { get; set; }

    /// <summary>The user this role is assigned to.</summary>
    public Guid UserId { get; set; }

    /// <summary>The user's role within this team in the Tracks module.</summary>
    public TracksTeamMemberRole Role { get; set; } = TracksTeamMemberRole.Member;

    /// <summary>Timestamp when the role was assigned (UTC).</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
