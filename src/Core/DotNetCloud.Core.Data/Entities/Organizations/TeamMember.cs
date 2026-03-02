using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Entities.Organizations;

/// <summary>
/// Represents a user's membership in a team.
/// Junction entity that connects users to teams with associated roles.
/// </summary>
/// <remarks>
/// Uses composite key (TeamId, UserId) to ensure each user appears only once per team.
/// Stores team-scoped roles that apply only within the context of this specific team.
/// Example: A user might be "TeamLead" in one team but "Member" in another.
/// </remarks>
public class TeamMember
{
    /// <summary>
    /// Gets or sets the unique identifier of the team.
    /// </summary>
    /// <remarks>
    /// Part of composite primary key. Required foreign key.
    /// </remarks>
    public Guid TeamId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the user.
    /// </summary>
    /// <remarks>
    /// Part of composite primary key. Required foreign key.
    /// </remarks>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the collection of role IDs assigned to this user within this team.
    /// </summary>
    /// <remarks>
    /// Stored as a serialized collection (JSON or CSV depending on database).
    /// Roles here are team-scoped and apply only within this team context.
    /// Example values: ["TeamLead"], ["Member", "Reviewer"], ["Admin"]
    /// Empty collection means the user is a basic team member with no special roles.
    /// </remarks>
    public ICollection<Guid> RoleIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the date and time when the user joined the team.
    /// </summary>
    /// <remarks>
    /// Automatically set when the membership is created.
    /// </remarks>
    public DateTime JoinedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the team that this membership belongs to.
    /// </summary>
    public Team Team { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user who is a member of this team.
    /// </summary>
    public ApplicationUser User { get; set; } = null!;
}
