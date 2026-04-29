using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A user's Tracks-specific role within a Core team.
/// </summary>
public sealed class TeamRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public TracksTeamMemberRole Role { get; set; } = TracksTeamMemberRole.Member;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
