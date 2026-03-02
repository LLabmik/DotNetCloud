using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Entities.Organizations;

/// <summary>
/// Represents a user's membership in an organization.
/// Junction entity that connects users to organizations with organization-scoped roles.
/// </summary>
/// <remarks>
/// Uses composite key (OrganizationId, UserId) to ensure each user appears only once per organization.
/// Stores organization-scoped roles that apply across all teams within the organization.
/// 
/// Role Hierarchy:
/// - Organization-scoped roles (stored here) = apply across entire organization
/// - Team-scoped roles (stored in TeamMember) = apply only within specific team
/// - Group membership (stored in GroupMember) = for cross-team permission groups
/// 
/// Example: A user might have "OrganizationAdmin" role here, plus "TeamLead" in TeamA and "Member" in TeamB.
/// </remarks>
public class OrganizationMember
{
    /// <summary>
    /// Gets or sets the unique identifier of the organization.
    /// </summary>
    /// <remarks>
    /// Part of composite primary key. Required foreign key.
    /// </remarks>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the user.
    /// </summary>
    /// <remarks>
    /// Part of composite primary key. Required foreign key.
    /// </remarks>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the collection of organization-scoped role IDs assigned to this user.
    /// </summary>
    /// <remarks>
    /// Stored as a serialized collection (JSON or CSV depending on database).
    /// Roles here are organization-scoped and apply across all teams in the organization.
    /// Example values: ["OrganizationAdmin"], ["BillingManager", "UserManager"], ["Member"]
    /// Empty collection means the user is a basic organization member with no special roles.
    /// </remarks>
    public ICollection<Guid> RoleIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the date and time when the user joined the organization.
    /// </summary>
    /// <remarks>
    /// Automatically set when the membership is created.
    /// Used for auditing and tracking membership history.
    /// </remarks>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the user who invited this member to the organization.
    /// </summary>
    /// <remarks>
    /// Optional. Tracks who added this user for audit purposes.
    /// Null if added by system, during initial setup, or self-registration.
    /// </remarks>
    public Guid? InvitedByUserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this membership is active.
    /// </summary>
    /// <remarks>
    /// Default is true. When false, the user still appears in the organization for audit purposes,
    /// but cannot access organization resources. Alternative to deleting the membership record.
    /// </remarks>
    public bool IsActive { get; set; } = true;

    // Navigation properties

    /// <summary>
    /// Gets or sets the organization that this membership belongs to.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user who is a member of this organization.
    /// </summary>
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user who invited this member to the organization.
    /// </summary>
    /// <remarks>
    /// Null if added by system, during initial setup, or self-registration.
    /// </remarks>
    public ApplicationUser? InvitedByUser { get; set; }
}
