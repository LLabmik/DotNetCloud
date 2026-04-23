namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides write operations for managing groups and their membership.
/// Modules use this capability to create, update, and delete groups, as well as manage members.
/// </summary>
/// <remarks>
/// <para><b>Capability Tier:</b> Restricted — requires admin approval before a module can use it.</para>
/// <para>
/// This capability complements <see cref="IGroupDirectory"/> (read-only) with write operations.
/// Group identity is owned by the core platform; modules extend groups with module-specific configuration.
/// </para>
/// </remarks>
public interface IGroupManager : ICapabilityInterface
{
    /// <summary>
    /// Creates a new group in the specified organization.
    /// </summary>
    /// <returns>The newly created group's info.</returns>
    Task<GroupInfo> CreateGroupAsync(Guid organizationId, string name, string? description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a group's name and/or description.
    /// </summary>
    /// <returns>The updated group info, or <c>null</c> if not found.</returns>
    Task<GroupInfo?> UpdateGroupAsync(Guid groupId, string? name, string? description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a group.
    /// </summary>
    /// <returns><c>true</c> if the group was deleted; <c>false</c> if not found.</returns>
    Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a user as a member of a group.
    /// </summary>
    /// <returns><c>true</c> if added; <c>false</c> if the user was already a member.</returns>
    Task<bool> AddMemberAsync(Guid groupId, Guid userId, Guid? addedByUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user from a group.
    /// </summary>
    /// <returns><c>true</c> if removed; <c>false</c> if the user was not a member.</returns>
    Task<bool> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
}