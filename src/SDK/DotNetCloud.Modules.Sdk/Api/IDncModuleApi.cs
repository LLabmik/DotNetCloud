using DotNetCloud.Core.Capabilities;

namespace DotNetCloud.Modules.Sdk.Api;

/// <summary>A minimal user profile returned by the core.</summary>
/// <param name="Id">The user id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="AvatarUrl">The avatar URL, when available.</param>
/// <param name="IsActive">Whether the account is active.</param>
/// <param name="Email">The email address; withheld unless the user consented to it.</param>
public sealed record DncModuleUser(
    string Id,
    string DisplayName,
    string AvatarUrl,
    bool IsActive,
    string Email);

/// <summary>The end user a module call is acting for.</summary>
/// <param name="UserId">The user id; empty when the module is acting for itself.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Email">The email address, when available.</param>
/// <param name="Locale">The user's locale.</param>
/// <param name="Timezone">The user's timezone.</param>
/// <param name="Roles">The user's roles, resolved by the core.</param>
public sealed record DncCurrentUser(
    string UserId,
    string DisplayName,
    string Email,
    string Locale,
    string Timezone,
    IReadOnlyList<string> Roles);

/// <summary>A notification a module asks the core to deliver.</summary>
/// <param name="Title">Title (bounded by the core).</param>
/// <param name="Body">Optional body.</param>
/// <param name="Category">Optional category, for example <c>Reminder</c>.</param>
/// <param name="Link">Optional site-relative action link.</param>
/// <param name="RecipientUserIds">Recipients.</param>
public sealed record DncNotification(
    string Title,
    string? Body,
    string? Category,
    string? Link,
    IReadOnlyList<string> RecipientUserIds);

/// <summary>
/// The core API surface a module uses, independent of transport.
/// </summary>
/// <remarks>
/// Implemented over gRPC (<see cref="DncModuleApiClient"/>) and JSON
/// (<see cref="DncModuleRestClient"/>), and faked in memory by the SDK test harness. Module code
/// should depend on this interface so it can be unit tested without a core process.
/// </remarks>
public interface IDncModuleApi
{
    /// <summary>Gets the end user the current call is acting for.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user context.</returns>
    Task<DncCurrentUser> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>Looks up one user's minimal profile.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The profile, or <see langword="null"/> when the user is unknown.</returns>
    Task<DncModuleUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Searches users.</summary>
    /// <param name="query">The search term.</param>
    /// <param name="maxResults">Maximum results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching profiles.</returns>
    Task<IReadOnlyList<DncModuleUser>> SearchUsersAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a notification through the core's delivery pipeline.</summary>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many recipients the core accepted.</returns>
    Task<int> SendNotificationAsync(DncNotification notification, CancellationToken cancellationToken = default);

    /// <summary>Publishes a platform event the module declared in its manifest.</summary>
    /// <param name="eventType">The event type name.</param>
    /// <param name="payloadJson">The payload JSON.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the core accepted the event.</returns>
    Task<bool> PublishEventAsync(
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default);

    /// <summary>Submits an entity to the core search index.</summary>
    /// <param name="entityId">The entity id.</param>
    /// <param name="remove">True to remove the entity from the index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the request was accepted.</returns>
    Task<bool> SubmitSearchIndexAsync(
        string entityId,
        bool remove = false,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a module setting. Keys are namespaced to the authenticated module.</summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value, or <see langword="null"/> when it is not set.</returns>
    Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a module setting. Keys are namespaced to the authenticated module.</summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the value was stored.</returns>
    Task<bool> SetSettingAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default);

    /// <summary>Writes an audit entry attributed to the authenticated module.</summary>
    /// <param name="action">The audit action.</param>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the entry was written.</returns>
    Task<bool> LogAuditAsync(
        AuditAction action,
        string entityType,
        Guid entityId,
        string? description = null,
        CancellationToken cancellationToken = default);
}
