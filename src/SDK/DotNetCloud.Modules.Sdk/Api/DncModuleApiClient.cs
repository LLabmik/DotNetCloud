using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using ModuleApiV1 = global::DotNetCloud.Core.Grpc.ModuleApi.V1;

namespace DotNetCloud.Modules.Sdk.Api;

/// <summary>
/// gRPC implementation of <see cref="IDncModuleApi"/> for the canonical
/// <c>dotnetcloud.moduleapi.v1.ModuleApi</c> service.
/// </summary>
/// <remarks>
/// Every call carries the supervisor-issued module credential through
/// <see cref="ModuleIdentityMetadata.Create(string, string?)"/>, which also attaches the ambient
/// end-user assertion for the request being served. There is deliberately no way to pass a caller or
/// module identity as an argument: the core ignores anything a payload claims.
/// </remarks>
public sealed class DncModuleApiClient : IDncModuleApi, IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly ModuleApiV1.ModuleApi.ModuleApiClient _client;
    private readonly ModuleEnvironment _environment;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="DncModuleApiClient"/> class.</summary>
    /// <param name="environment">The module's runtime environment.</param>
    /// <param name="channel">Optional pre-built channel (tests, custom transports).</param>
    public DncModuleApiClient(ModuleEnvironment environment, GrpcChannel? channel = null)
    {
        ArgumentNullException.ThrowIfNull(environment);

        _environment = environment;
        _channel = channel ?? GrpcChannel.ForAddress(environment.CoreEndpoint);
        _client = new ModuleApiV1.ModuleApi.ModuleApiClient(_channel);
    }

    /// <summary>
    /// Creates a client from the environment the supervisor provides.
    /// </summary>
    /// <returns>The client.</returns>
    public static DncModuleApiClient CreateFromEnvironment() =>
        new(ModuleEnvironment.FromEnvironment());

    /// <inheritdoc />
    public async Task<DncCurrentUser> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client.GetCurrentUserContextAsync(
            new ModuleApiV1.GetCurrentUserContextRequest(),
            Metadata(),
            cancellationToken: cancellationToken);

        return new DncCurrentUser(
            response.UserId,
            response.DisplayName,
            response.Email,
            response.Locale,
            response.Timezone,
            response.Roles.ToArray());
    }

    /// <inheritdoc />
    public async Task<DncModuleUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetUserAsync(
            new ModuleApiV1.GetUserRequest { UserId = userId },
            Metadata(),
            cancellationToken: cancellationToken);

        return response.Found && response.User is not null ? ToUser(response.User) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DncModuleUser>> SearchUsersAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.SearchUsersAsync(
            new ModuleApiV1.SearchUsersRequest { Query = query, MaxResults = maxResults },
            Metadata(),
            cancellationToken: cancellationToken);

        return response.Users.Select(ToUser).ToArray();
    }

    /// <inheritdoc />
    public async Task<int> SendNotificationAsync(
        DncNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var request = new ModuleApiV1.SendNotificationRequest
        {
            Title = notification.Title,
            Body = notification.Body ?? string.Empty,
            Category = notification.Category ?? string.Empty,
            Link = notification.Link ?? string.Empty,
        };
        request.RecipientUserIds.AddRange(notification.RecipientUserIds);

        var response = await _client.SendNotificationAsync(
            request, Metadata(), cancellationToken: cancellationToken);

        return response.DeliveredCount;
    }

    /// <inheritdoc />
    public async Task<bool> PublishEventAsync(
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.PublishEventAsync(
            new ModuleApiV1.PublishEventRequest { EventType = eventType, PayloadJson = payloadJson },
            Metadata(),
            cancellationToken: cancellationToken);

        return response.Success;
    }

    /// <inheritdoc />
    public async Task<bool> SubmitSearchIndexAsync(
        string entityId,
        bool remove = false,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.SubmitSearchIndexAsync(
            new ModuleApiV1.SubmitSearchIndexRequest { EntityId = entityId, Action = remove ? 1 : 0 },
            Metadata(),
            cancellationToken: cancellationToken);

        return response.Success;
    }

    /// <inheritdoc />
    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetSettingAsync(
            new ModuleApiV1.GetSettingRequest { Key = key },
            Metadata(),
            cancellationToken: cancellationToken);

        return response.Found ? response.Value : null;
    }

    /// <inheritdoc />
    public async Task<bool> SetSettingAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.SetSettingAsync(
            new ModuleApiV1.SetSettingRequest { Key = key, Value = value },
            Metadata(),
            cancellationToken: cancellationToken);

        return response.Success;
    }

    /// <inheritdoc />
    public async Task<bool> LogAuditAsync(
        AuditAction action,
        string entityType,
        Guid entityId,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.LogAuditAsync(
            new ModuleApiV1.LogAuditRequest
            {
                Action = (int)action,
                EntityType = entityType,
                EntityId = entityId.ToString(),
                Description = description ?? string.Empty,
            },
            Metadata(),
            cancellationToken: cancellationToken);

        return response.Success;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _channel.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DncFileNode>> ListFilesAsync(
        string? parentId = null,
        int page = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.ListFilesAsync(
            new ModuleApiV1.ListFilesRequest
            {
                ParentId = parentId ?? string.Empty,
                Page = page,
                PageSize = pageSize,
            },
            Metadata(),
            cancellationToken: cancellationToken);

        return response.Nodes.Select(ToFileNode).ToArray();
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadFileAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        // Spooled so the caller owns a seekable stream that is independent of the call's lifetime,
        // and so a large attachment is not pinned in the heap.
        var path = Path.Combine(Path.GetTempPath(), $"dnc-sdk-file-{Guid.CreateVersion7():N}.tmp");
        var spool = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        try
        {
            using var call = _client.DownloadFile(
                new ModuleApiV1.DownloadFileRequest { NodeId = nodeId },
                Metadata(),
                cancellationToken: cancellationToken);

            await foreach (var chunk in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                if (chunk.Data.Length > 0)
                {
                    await spool.WriteAsync(chunk.Data.Memory, cancellationToken);
                }
            }

            spool.Position = 0;
            return spool;
        }
        catch
        {
            await spool.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DncFileNode> UploadFileAsync(
        string? parentId,
        string fileName,
        string contentType,
        Stream content,
        long totalSize = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        using var call = _client.UploadFile(Metadata(), cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(
            new ModuleApiV1.UploadFileRequest
            {
                Metadata = new ModuleApiV1.ModuleFileUploadMetadata
                {
                    ParentId = parentId ?? string.Empty,
                    FileName = fileName,
                    MimeType = contentType ?? string.Empty,
                    TotalSize = totalSize,
                },
            },
            cancellationToken);

        var buffer = new byte[64 * 1024];
        int read;

        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await call.RequestStream.WriteAsync(
                new ModuleApiV1.UploadFileRequest
                {
                    Chunk = new ModuleApiV1.FileContentChunk
                    {
                        Data = Google.Protobuf.ByteString.CopyFrom(buffer, 0, read),
                    },
                },
                cancellationToken);
        }

        await call.RequestStream.CompleteAsync();

        var response = await call.ResponseAsync;

        if (response.Node is null)
        {
            throw new DncModuleApiException(
                "MODULE_API_EMPTY_RESPONSE",
                "The core accepted the upload but returned no file node.",
                statusCode: 200);
        }

        return ToFileNode(response.Node);
    }

    private Metadata Metadata() => _environment.CreateMetadata();

    private static DncFileNode ToFileNode(ModuleApiV1.ModuleFileNode node) => new(
        node.Id,
        node.Name,
        node.NodeType,
        node.MimeType,
        node.Size,
        node.ParentId,
        node.UpdatedAt);

    private static DncModuleUser ToUser(ModuleApiV1.ModuleUserProfile user) => new(
        user.Id,
        user.DisplayName,
        user.AvatarUrl,
        user.IsActive,
        user.Email);
}
