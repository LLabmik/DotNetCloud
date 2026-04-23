namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read access to organization group and group membership information.
/// Modules use this capability to resolve group membership and group metadata.
/// </summary>
/// <remarks>
/// <para><b>Capability Tier:</b> Restricted — requires admin approval before a module can use it.</para>
/// <para>
/// This interface is read-only. Group management (create, update, delete, member management)
/// is handled by the core platform directly. Modules consume group data for authorization
/// and UI purposes.
/// </para>
/// </remarks>
public interface IGroupDirectory : ICapabilityInterface
{
    /// <summary>
    /// Gets basic group information by ID.
    /// </summary>
    /// <returns>The group info if found; otherwise <c>null</c>.</returns>
    Task<GroupInfo?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all groups defined within an organization.
    /// </summary>
    Task<IReadOnlyList<GroupInfo>> GetGroupsForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all groups a user is a member of.
    /// </summary>
    Task<IReadOnlyList<GroupInfo>> GetGroupsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a user is a member of a specific group.
    /// </summary>
    Task<bool> IsGroupMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user's membership details on a group.
    /// </summary>
    /// <returns>The membership info if the user is a member; otherwise <c>null</c>.</returns>
    Task<GroupMemberInfo?> GetGroupMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all members of a group.
    /// </summary>
    Task<IReadOnlyList<GroupMemberInfo>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight read-only group info returned by <see cref="IGroupDirectory"/>.
/// </summary>
public sealed record GroupInfo
{
    /// <summary>Group ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Organization this group belongs to.</summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>Group name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Whether this is the built-in organization-wide group.</summary>
    public bool IsAllUsersGroup { get; init; }

    /// <summary>Number of members.</summary>
    public int MemberCount { get; init; }

    /// <summary>When the group was created.</summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Lightweight read-only group member info returned by <see cref="IGroupDirectory"/>.
/// </summary>
public sealed record GroupMemberInfo
{
    /// <summary>The group ID.</summary>
    public required Guid GroupId { get; init; }

    /// <summary>The user ID.</summary>
    public required Guid UserId { get; init; }

    /// <summary>When the user was added to the group.</summary>
    public required DateTime AddedAt { get; init; }

    /// <summary>The user who added this member, if recorded.</summary>
    public Guid? AddedByUserId { get; init; }
}