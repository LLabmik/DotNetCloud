namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Resolves a user's primary organization ID. Used by background services
/// (e.g., trash cleanup) to apply per-organization policy overrides.
/// </summary>
/// <remarks>
/// Implemented by the core server, which has access to the organization
/// membership data. Injected into the Files module at startup.
/// </remarks>
public interface IUserOrganizationResolver
{
    /// <summary>
    /// Gets the primary organization ID for a user, or <c>null</c> if the user
    /// does not belong to any organization.
    /// </summary>
    Task<Guid?> GetOrganizationIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the primary organization IDs for a batch of user IDs.
    /// Users without an organization are omitted from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetOrganizationIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}
