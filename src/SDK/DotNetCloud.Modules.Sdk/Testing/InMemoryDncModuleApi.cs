using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Sdk.Api;

namespace DotNetCloud.Modules.Sdk.Testing;

/// <summary>
/// In-memory <see cref="Api.IDncModuleApi"/> for module unit tests.
/// </summary>
/// <remarks>
/// Module code that depends on <see cref="Api.IDncModuleApi"/> can be exercised without a core
/// process, a database or a credential. Every call is recorded so a test can assert what the module
/// asked the platform to do, and the user directory is configurable so identity-dependent branches
/// are easy to cover.
/// </remarks>
public sealed class InMemoryDncModuleApi : Api.IDncModuleApi
{
    private readonly Dictionary<string, string> _settings = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the user every call is acting for.</summary>
    public DncCurrentUserUser CurrentUser { get; set; } =
        new DncCurrentUserUser(Guid.CreateVersion7().ToString(), "Test User", "test@example.com", "en-US", "UTC", ["User"]);

    /// <summary>Gets the users the module can look up, keyed by id.</summary>
    public Dictionary<string, Api.DncModuleUser> Users { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the notifications the module sent.</summary>
    public List<Api.DncNotification> Notifications { get; } = [];

    /// <summary>Gets the events the module published, as (type, payload) pairs.</summary>
    public List<(string EventType, string PayloadJson)> PublishedEvents { get; } = [];

    /// <summary>Gets the search-index submissions, as (entity id, removed) pairs.</summary>
    public List<(string EntityId, bool Removed)> SearchSubmissions { get; } = [];

    /// <summary>Gets the audit entries the module wrote.</summary>
    public List<(AuditAction Action, string EntityType, Guid EntityId, string? Description)> AuditEntries { get; } = [];

    /// <summary>Gets or sets a value indicating whether published events are accepted.</summary>
    public bool AcceptEvents { get; set; } = true;

    /// <summary>Gets the simulated file tree, keyed by node id.</summary>
    public Dictionary<string, Api.DncFileNode> Files { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the content of the simulated files, keyed by node id.</summary>
    public Dictionary<string, byte[]> FileContents { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the uploads the module performed, as (parent, name, content) tuples.</summary>
    public List<(string? ParentId, string FileName, byte[] Content)> UploadedFiles { get; } = [];

    /// <inheritdoc />
    public Task<Api.DncCurrentUser> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new Api.DncCurrentUser(
            CurrentUser.UserId,
            CurrentUser.DisplayName,
            CurrentUser.Email,
            CurrentUser.Locale,
            CurrentUser.Timezone,
            CurrentUser.Roles));

    /// <inheritdoc />
    public Task<Api.DncModuleUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.TryGetValue(userId, out var user) ? user : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<Api.DncModuleUser>> SearchUsersAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var matches = Users.Values
            .Where(user => user.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || user.Email.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Api.DncModuleUser>>(matches);
    }

    /// <inheritdoc />
    public Task<int> SendNotificationAsync(
        Api.DncNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        Notifications.Add(notification);
        return Task.FromResult(notification.RecipientUserIds.Count);
    }

    /// <inheritdoc />
    public Task<bool> PublishEventAsync(
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        if (!AcceptEvents)
        {
            return Task.FromResult(false);
        }

        PublishedEvents.Add((eventType, payloadJson));
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> SubmitSearchIndexAsync(
        string entityId,
        bool remove = false,
        CancellationToken cancellationToken = default)
    {
        SearchSubmissions.Add((entityId, remove));
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_settings.TryGetValue(key, out var value) ? value : null);

    /// <inheritdoc />
    public Task<bool> SetSettingAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        _settings[key] = value;
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> LogAuditAsync(
        AuditAction action,
        string entityType,
        Guid entityId,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        AuditEntries.Add((action, entityType, entityId, description));
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Api.DncFileNode>> ListFilesAsync(
        string? parentId = null,
        int page = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        var matches = Files.Values
            .Where(node => string.Equals(node.ParentId, parentId ?? string.Empty, StringComparison.Ordinal))
            .OrderBy(node => node.NodeType == "Folder" ? 0 : 1)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .Skip((Math.Max(page, 1) - 1) * Math.Max(pageSize, 1))
            .Take(Math.Max(pageSize, 1))
            .ToArray();

        return Task.FromResult<IReadOnlyList<Api.DncFileNode>>(matches);
    }

    /// <inheritdoc />
    public Task<Stream> DownloadFileAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!FileContents.TryGetValue(nodeId, out var content))
        {
            return Task.FromException<Stream>(new DncModuleApiException(
                "FILE_NOT_FOUND",
                "The simulated file has no content.",
                404));
        }

        return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
    }

    /// <inheritdoc />
    public async Task<Api.DncFileNode> UploadFileAsync(
        string? parentId,
        string fileName,
        string contentType,
        Stream content,
        long totalSize = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var bytes = buffer.ToArray();
        var node = new Api.DncFileNode(
            Guid.CreateVersion7().ToString(),
            fileName,
            "File",
            contentType ?? string.Empty,
            bytes.Length,
            parentId ?? string.Empty,
            DateTimeOffset.UtcNow.ToString("O"));

        Files[node.Id] = node;
        FileContents[node.Id] = bytes;
        UploadedFiles.Add((parentId, fileName, bytes));

        return node;
    }
}

/// <summary>
/// Mutable shape of the end user a test harness acts for.
/// </summary>
/// <param name="UserId">The user id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Email">The email address.</param>
/// <param name="Locale">The locale.</param>
/// <param name="Timezone">The timezone.</param>
/// <param name="Roles">The roles.</param>
public sealed record DncCurrentUserUser(
    string UserId,
    string DisplayName,
    string Email,
    string Locale,
    string Timezone,
    IReadOnlyList<string> Roles);
