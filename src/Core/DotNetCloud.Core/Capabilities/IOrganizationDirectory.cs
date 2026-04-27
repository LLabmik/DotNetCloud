namespace DotNetCloud.Core.Capabilities;

using DotNetCloud.Core.DTOs;

/// <summary>
/// Provides access to organization membership information.
/// Modules use this capability to check whether a user belongs to an organization
/// and to resolve their organization-scoped roles.
/// </summary>
/// <remarks>
/// <para><b>Capability Tier:</b> Restricted — requires admin approval before a module can use it.</para>
/// <para>
/// This interface is read-only. Organization management (create, update, delete, member management)
/// is handled by the core platform directly. Modules consume organization data for authorization
/// and UI purposes.
/// </para>
/// </remarks>
public interface IOrganizationDirectory : ICapabilityInterface
{
    /// <summary>
    /// Checks whether a user is a member of a specific organization.
    /// </summary>
    /// <returns><c>true</c> if the user is an active member; otherwise <c>false</c>.</returns>
    Task<bool> IsOrganizationMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the membership info for a user in an organization.
    /// </summary>
    /// <returns>The membership info if found; otherwise <c>null</c>.</returns>
    Task<OrganizationMemberInfo?> GetMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
}
