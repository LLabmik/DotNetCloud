namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read-only access to user directory data.
/// Modules use this capability to resolve usernames and retrieve display names.
/// </summary>
public interface IUserDirectory : ICapabilityInterface
{
    /// <summary>
    /// Finds a user's ID by their username (case-insensitive).
    /// </summary>
    /// <returns>The user ID if found; otherwise <c>null</c>.</returns>
    Task<Guid?> FindUserIdByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets display names for a batch of user IDs.
    /// IDs that do not map to a user are omitted from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}
