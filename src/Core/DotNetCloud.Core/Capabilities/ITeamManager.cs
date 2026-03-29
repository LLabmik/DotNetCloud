namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides write operations for managing teams and their membership.
/// Modules use this capability to create, update, and delete teams, as well as manage members.
/// </summary>
/// <remarks>
/// <para><b>Capability Tier:</b> Restricted — requires admin approval before a module can use it.</para>
/// <para>
/// This capability complements <see cref="ITeamDirectory"/> (read-only) with write operations.
/// Team identity is owned by the core platform; modules extend teams with module-specific configuration.
/// </para>
/// </remarks>
public interface ITeamManager : ICapabilityInterface
{
    /// <summary>
    /// Creates a new team in the specified organization.
    /// </summary>
    /// <returns>The newly created team's info.</returns>
    Task<TeamInfo> CreateTeamAsync(Guid organizationId, string name, string? description, Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a team's name and/or description.
    /// </summary>
    /// <returns>The updated team info, or <c>null</c> if not found.</returns>
    Task<TeamInfo?> UpdateTeamAsync(Guid teamId, string? name, string? description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a team.
    /// </summary>
    /// <returns><c>true</c> if the team was deleted; <c>false</c> if not found.</returns>
    Task<bool> DeleteTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a user as a member of a team.
    /// </summary>
    /// <returns><c>true</c> if added; <c>false</c> if the user was already a member.</returns>
    Task<bool> AddMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user from a team.
    /// </summary>
    /// <returns><c>true</c> if removed; <c>false</c> if the user was not a member.</returns>
    Task<bool> RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);
}
