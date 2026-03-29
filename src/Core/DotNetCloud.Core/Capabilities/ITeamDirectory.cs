namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read access to team and membership information.
/// Modules use this capability to check team membership and resolve team metadata.
/// </summary>
/// <remarks>
/// <para><b>Capability Tier:</b> Restricted — requires admin approval before a module can use it.</para>
/// <para>
/// This interface is read-only. Team management (create, update, delete, member management)
/// is handled by the core platform directly. Modules consume team data for authorization
/// and UI purposes.
/// </para>
/// </remarks>
public interface ITeamDirectory : ICapabilityInterface
{
    /// <summary>
    /// Gets basic team information by ID.
    /// </summary>
    /// <returns>The team info if found; otherwise <c>null</c>.</returns>
    Task<TeamInfo?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all teams a user is a member of.
    /// </summary>
    Task<IReadOnlyList<TeamInfo>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a user is a member of a specific team.
    /// </summary>
    Task<bool> IsTeamMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user's membership details on a team, including their role IDs.
    /// </summary>
    /// <returns>The membership info if the user is a member; otherwise <c>null</c>.</returns>
    Task<TeamMemberInfo?> GetTeamMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all members of a team.
    /// </summary>
    Task<IReadOnlyList<TeamMemberInfo>> GetTeamMembersAsync(Guid teamId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight read-only team info returned by <see cref="ITeamDirectory"/>.
/// </summary>
public sealed record TeamInfo
{
    /// <summary>Team ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Organization this team belongs to.</summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>Team name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Number of members.</summary>
    public int MemberCount { get; init; }

    /// <summary>When the team was created.</summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Lightweight read-only team member info returned by <see cref="ITeamDirectory"/>.
/// </summary>
public sealed record TeamMemberInfo
{
    /// <summary>The team ID.</summary>
    public required Guid TeamId { get; init; }

    /// <summary>The user ID.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Role IDs assigned to this user within the team.</summary>
    public IReadOnlyList<Guid> RoleIds { get; init; } = [];

    /// <summary>When the user joined the team.</summary>
    public required DateTime JoinedAt { get; init; }
}
