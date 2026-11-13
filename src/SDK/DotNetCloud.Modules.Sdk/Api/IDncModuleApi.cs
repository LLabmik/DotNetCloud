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

/// <summary>One entry in the acting user's file list.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Name">Display name.</param>
/// <param name="NodeType"><c>File</c>, <c>Folder</c> or <c>SymbolicLink</c>.</param>
/// <param name="MimeType">MIME type; empty for folders.</param>
/// <param name="Size">Size in bytes; 0 for folders.</param>
/// <param name="ParentId">Parent folder id; empty at the root of the user's files.</param>
/// <param name="UpdatedAt">RFC 3339 timestamp of the last content change; empty when unknown.</param>
public sealed record DncFileNode(
    string Id,
    string Name,
    string NodeType,
    string MimeType,
    long Size,
    string ParentId,
    string UpdatedAt);

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

    /// <summary>
    /// Lists one folder of the acting user's files. Requires the <c>IFileDirectory</c> capability and
    /// the user's consent to <c>files:read</c>.
    /// </summary>
    /// <param name="parentId">Folder to list; <see langword="null"/> lists the user's root.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Requested page size (the core caps it).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder's children.</returns>
    Task<IReadOnlyList<DncFileNode>> ListFilesAsync(
        string? parentId = null,
        int page = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the current content of one of the acting user's files. Requires <c>files:read</c> consent.
    /// </summary>
    /// <param name="nodeId">The file node id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A readable stream the caller owns and must dispose.</returns>
    Task<Stream> DownloadFileAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a file in the acting user's files. Requires the <c>IFileWriter</c> capability and the
    /// user's consent to <c>files:write</c>.
    /// </summary>
    /// <param name="parentId">Target folder; <see langword="null"/> stores in the user's root.</param>
    /// <param name="fileName">File name (sanitized by the core).</param>
    /// <param name="contentType">MIME type.</param>
    /// <param name="content">The content, read to the end.</param>
    /// <param name="totalSize">Declared size in bytes; 0 when unknown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored node.</returns>
    Task<DncFileNode> UploadFileAsync(
        string? parentId,
        string fileName,
        string contentType,
        Stream content,
        long totalSize = 0,
        CancellationToken cancellationToken = default);
}
