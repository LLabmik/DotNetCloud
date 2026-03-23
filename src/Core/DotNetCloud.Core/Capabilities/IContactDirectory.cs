namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read-only access to the contact directory.
/// Modules use this capability to resolve contacts by user ID without direct data access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability tier:</b> Public — automatically granted to all modules.
/// </para>
/// <para>
/// This capability exposes a read-only view of contacts. Modules that need to
/// create or modify contacts must use the Contacts module API directly.
/// </para>
/// </remarks>
public interface IContactDirectory : ICapabilityInterface
{
    /// <summary>
    /// Gets a contact by its unique identifier.
    /// </summary>
    /// <param name="contactId">The contact ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The contact display name if found; otherwise <c>null</c>.</returns>
    Task<string?> GetContactDisplayNameAsync(Guid contactId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets display names for a batch of contact IDs.
    /// IDs that do not map to a contact are omitted from the result.
    /// </summary>
    /// <param name="contactIds">The contact IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetContactDisplayNamesAsync(
        IEnumerable<Guid> contactIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches contacts owned by a user by display name (case-insensitive substring match).
    /// </summary>
    /// <param name="userId">The owner's user ID.</param>
    /// <param name="query">Search query string.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Contact IDs and display names matching the query.</returns>
    Task<IReadOnlyList<(Guid ContactId, string DisplayName)>> SearchContactsAsync(
        Guid userId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}
