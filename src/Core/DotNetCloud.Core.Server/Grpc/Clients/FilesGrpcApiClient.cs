using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using DotNetCloud.Core;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Files.Host.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Files gRPC client used by the Core Server.
/// </summary>
public sealed class FilesGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "FilesGrpc";
    /// <summary>The gRPC address of the Files module.</summary>
    public string FilesModuleAddress { get; set; } = "http://localhost:5004";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="IFilesApiClient"/>.
/// </summary>
/// <remarks>
/// Calls carry the supervisor-issued module credential (attached by the managed channel) plus a
/// core-signed user assertion naming the owner the call is made for. The Files host requires both:
/// the credential proves the core is calling, and the assertion is the only way an operation can name
/// a user, so the owner id in the payload cannot be spoofed by another process.
/// </remarks>
public sealed class FilesGrpcApiClient : IFilesApiClient
{
    /// <summary>The module id of the Files module.</summary>
    internal const string FilesModuleId = "dotnetcloud.files";

    /// <summary>Chunk size for uploads; matches the Files module's default content chunking.</summary>
    internal const int UploadChunkSize = 4 * 1024 * 1024;

    private readonly FilesGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly Supervisor.IModuleGrpcChannelProvider _channelManager;
    private readonly Supervisor.ModuleAssertionKeyRing _assertions;
    private readonly ILogger<FilesGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;

    /// <summary>Initializes a new instance of the <see cref="FilesGrpcApiClient"/> class.</summary>
    public FilesGrpcApiClient(
        IOptions<FilesGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        Supervisor.IModuleGrpcChannelProvider channelManager,
        Supervisor.ModuleAssertionKeyRing assertions,
        ILogger<FilesGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _channelManager = channelManager;
        _assertions = assertions;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.files");
        _logger.LogInformation("FilesGrpcApiClient connecting to {Address}", address);

        // Managed channel: it attaches the supervisor-issued credential the Files host requires.
        return _channelManager.GetOrCreateChannel("dotnetcloud.files", address);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <inheritdoc />
    public async Task<FileListResult> ListChildrenAsync(
        Guid ownerId,
        Guid? parentId,
        int page = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new FilesService.FilesServiceClient(_channel.Value);

            var response = await client.ListNodesAsync(
                new ListNodesRequest
                {
                    UserId = ownerId.ToString(),
                    ParentId = parentId?.ToString() ?? string.Empty,
                    Page = page > 0 ? page : 1,
                    PageSize = pageSize > 0 ? pageSize : 200,
                },
                UserHeaders(ownerId, "files.read"),
                deadline: DateTime.UtcNow.Add(_options.Timeout),
                cancellationToken: cancellationToken);

            return new FileListResult
            {
                Status = FilesAccessStatus.Success,
                TotalCount = response.TotalCount,
                Nodes = ProjectNodes(response.Nodes),
            };
        }
        catch (RpcException ex)
        {
            return new FileListResult { Status = MapFailure(ex, "list files"), ErrorMessage = ex.Status.Detail };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to list files of {UserId}", ownerId);
            return new FileListResult { Status = FilesAccessStatus.Failed, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<FileReadResult> OpenReadAsync(
        Guid ownerId,
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        // Content is spooled to a temp file instead of a MemoryStream: an attachment may be tens of
        // megabytes and several can be in flight at once, so the request must not pin that in the heap.
        FileStream? spool = null;

        try
        {
            var client = new FilesService.FilesServiceClient(_channel.Value);

            var path = Path.Combine(Path.GetTempPath(), $"dnc-file-{Guid.CreateVersion7():N}.tmp");
            spool = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

            using var call = client.DownloadFile(
                new DownloadFileRequest { UserId = ownerId.ToString(), NodeId = nodeId.ToString() },
                UserHeaders(ownerId, "files.read"),
                deadline: DateTime.UtcNow.Add(_options.Timeout),
                cancellationToken: cancellationToken);

            await foreach (var message in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                if (message.ChunkData.Length > 0)
                {
                    await spool.WriteAsync(message.ChunkData.Memory, cancellationToken);
                }
            }

            spool.Position = 0;

            var result = spool;
            spool = null;
            return new FileReadResult { Status = FilesAccessStatus.Success, Content = result };
        }
        catch (RpcException ex)
        {
            return new FileReadResult { Status = MapFailure(ex, "read file"), ErrorMessage = ex.Status.Detail };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to read file {NodeId} of {UserId}", nodeId, ownerId);
            return new FileReadResult { Status = FilesAccessStatus.Failed, ErrorMessage = ex.Message };
        }
        finally
        {
            if (spool is not null)
            {
                await spool.DisposeAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task<FileWriteResult> UploadFileAsync(
        Guid ownerId,
        Guid? parentId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        FileStream? buffer = null;

        try
        {
            // Two passes over a temp spool: the first computes the chunk manifest the module requires
            // before it will open a session, the second sends only the chunks it does not already hold.
            var path = Path.Combine(Path.GetTempPath(), $"dnc-upload-{Guid.CreateVersion7():N}.tmp");
            buffer = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

            var chunkHashes = new List<string>();
            var chunkSizes = new List<int>();

            var chunk = ArrayPool<byte>.Shared.Rent(UploadChunkSize);
            try
            {
                var filled = 0;
                int read;
                while ((read = await content.ReadAsync(chunk.AsMemory(filled, UploadChunkSize - filled), cancellationToken)) > 0)
                {
                    filled += read;

                    if (filled == UploadChunkSize)
                    {
                        await buffer.WriteAsync(chunk.AsMemory(0, filled), cancellationToken);
                        chunkHashes.Add(Convert.ToHexStringLower(SHA256.HashData(chunk.AsSpan(0, filled))));
                        chunkSizes.Add(filled);
                        filled = 0;
                    }
                }

                if (filled > 0)
                {
                    await buffer.WriteAsync(chunk.AsMemory(0, filled), cancellationToken);
                    chunkHashes.Add(Convert.ToHexStringLower(SHA256.HashData(chunk.AsSpan(0, filled))));
                    chunkSizes.Add(filled);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(chunk);
            }

            var totalSize = chunkSizes.Sum(size => (long)size);
            await buffer.FlushAsync(cancellationToken);

            var client = new FilesService.FilesServiceClient(_channel.Value);
            var headers = UserHeaders(ownerId, "files.write");

            var initiate = await client.InitiateUploadAsync(
                new InitiateUploadRequest
                {
                    UserId = ownerId.ToString(),
                    ParentId = parentId?.ToString() ?? string.Empty,
                    FileName = fileName,
                    MimeType = contentType,
                    TotalSize = totalSize,
                    ChunkHashes = { chunkHashes },
                    ChunkSizes = { chunkSizes },
                },
                headers,
                deadline: DateTime.UtcNow.Add(_options.Timeout),
                cancellationToken: cancellationToken);

            if (!initiate.Success)
            {
                return new FileWriteResult
                {
                    Status = FilesAccessStatus.Failed,
                    ErrorMessage = string.IsNullOrEmpty(initiate.ErrorMessage)
                        ? "The Files module refused the upload."
                        : initiate.ErrorMessage,
                };
            }

            var missing = initiate.MissingChunks
                .Select(hash => hash.ToLowerInvariant())
                .ToHashSet(StringComparer.Ordinal);

            if (missing.Count > 0)
            {
                // Offsets are computed once: reading the spool per chunk with Take/Sum would make a
                // large upload quadratic in the number of chunks.
                var offsets = new long[chunkSizes.Count];
                for (var index = 1; index < chunkSizes.Count; index++)
                {
                    offsets[index] = offsets[index - 1] + chunkSizes[index - 1];
                }

                var sendBuffer = ArrayPool<byte>.Shared.Rent(UploadChunkSize);
                try
                {
                    for (var index = 0; index < chunkHashes.Count; index++)
                    {
                        if (!missing.Contains(chunkHashes[index]))
                        {
                            continue;
                        }

                        var size = chunkSizes[index];
                        buffer.Position = offsets[index];
                        await buffer.ReadExactlyAsync(sendBuffer.AsMemory(0, size), cancellationToken);

                        var uploaded = await client.UploadChunkAsync(
                            new UploadChunkRequest
                            {
                                SessionId = initiate.SessionId,
                                ChunkHash = chunkHashes[index],
                                ChunkData = Google.Protobuf.ByteString.CopyFrom(sendBuffer, 0, size),
                            },
                            headers,
                            deadline: DateTime.UtcNow.Add(_options.Timeout),
                            cancellationToken: cancellationToken);

                        if (!uploaded.Success)
                        {
                            return new FileWriteResult
                            {
                                Status = FilesAccessStatus.Failed,
                                ErrorMessage = string.IsNullOrEmpty(uploaded.ErrorMessage)
                                    ? "The Files module rejected a content chunk."
                                    : uploaded.ErrorMessage,
                            };
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(sendBuffer);
                }
            }

            var complete = await client.CompleteUploadAsync(
                new CompleteUploadRequest { SessionId = initiate.SessionId, UserId = ownerId.ToString() },
                headers,
                deadline: DateTime.UtcNow.Add(_options.Timeout),
                cancellationToken: cancellationToken);

            if (!complete.Success || complete.Node is null)
            {
                return new FileWriteResult
                {
                    Status = FilesAccessStatus.Failed,
                    ErrorMessage = string.IsNullOrEmpty(complete.ErrorMessage)
                        ? "The Files module did not finish the upload."
                        : complete.ErrorMessage,
                };
            }

            return new FileWriteResult
            {
                Status = FilesAccessStatus.Success,
                Node = ToFileNodeInfo(complete.Node),
            };
        }
        catch (RpcException ex)
        {
            return new FileWriteResult { Status = MapFailure(ex, "save file"), ErrorMessage = ex.Status.Detail };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to store {FileName} in the files of {UserId}", fileName, ownerId);
            return new FileWriteResult { Status = FilesAccessStatus.Failed, ErrorMessage = ex.Message };
        }
        finally
        {
            if (buffer is not null)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    private Metadata UserHeaders(Guid ownerId, string operation) =>
        new()
        {
            { ModuleIdentityMetadata.UserAssertionHeader, _assertions.Mint(ownerId, FilesModuleId, operation) },
        };

    private FilesAccessStatus MapFailure(RpcException exception, string operation)
    {
        if (exception.StatusCode is StatusCode.Unavailable or StatusCode.Unimplemented)
        {
            _logger.LogWarning(
                "Files module is unavailable while trying to {Operation}: {Detail}",
                operation,
                LogSanitizer.Sanitize(exception.Status.Detail ?? string.Empty));
        }
        else
        {
            // Never log the assertion or file name here: both identify the object the user acted on.
            _logger.LogWarning(
                "Files module refused to {Operation}: {StatusCode} {Detail}",
                operation,
                exception.StatusCode,
                LogSanitizer.Sanitize(exception.Status.Detail ?? string.Empty));
        }

        return exception.StatusCode switch
        {
            StatusCode.NotFound => FilesAccessStatus.NotFound,
            StatusCode.Unavailable => FilesAccessStatus.Unavailable,
            StatusCode.Unimplemented => FilesAccessStatus.Unavailable,
            _ => FilesAccessStatus.Failed,
        };
    }

    private static IReadOnlyList<FileNodeInfo> ProjectNodes(IEnumerable<FileNodeMessage> nodes)
    {
        var projected = new List<FileNodeInfo>();

        foreach (var node in nodes)
        {
            if (ToFileNodeInfo(node) is { } info)
            {
                projected.Add(info);
            }
        }

        return projected;
    }

    private static FileNodeInfo? ToFileNodeInfo(FileNodeMessage node)
    {
        if (!Guid.TryParse(node.Id, out var id))
        {
            return null;
        }

        return new FileNodeInfo
        {
            Id = id,
            Name = node.Name,
            NodeType = string.IsNullOrEmpty(node.NodeType) ? "File" : node.NodeType,
            MimeType = string.IsNullOrEmpty(node.MimeType) ? null : node.MimeType,
            Size = node.Size,
            ParentId = Guid.TryParse(node.ParentId, out var parentId) ? parentId : null,
            UpdatedAt = DateTimeOffset.TryParse(node.UpdatedAt, out var updatedAt) ? updatedAt : null,
        };
    }

    /// <inheritdoc />
    public async Task<MediaScanCandidatesResult> ScanMediaFoldersAsync(
        IReadOnlyCollection<MediaLibrarySource> sources,
        Guid ownerId,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new FilesService.FilesServiceClient(_channel.Value);
            var sourcesJson = JsonSerializer.Serialize(sources, JsonOptions);

            var request = new ScanMediaFoldersRequest
            {
                SourcesJson = sourcesJson,
                UserId = ownerId.ToString(),
                MediaType = mediaType,
            };

            var response = await client.ScanMediaFoldersAsync(request,
                deadline: DateTime.UtcNow.Add(_options.Timeout),
                cancellationToken: cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("Media folder scan failed: {Error}", response.ErrorMessage);
                return new MediaScanCandidatesResult
                {
                    Success = false,
                    ErrorMessage = response.ErrorMessage,
                };
            }

            var candidates = response.Candidates
                .Select(c => new MediaFileCandidateDto
                {
                    Id = Guid.TryParse(c.Id, out var id) ? id : Guid.Empty,
                    Name = c.Name,
                    Size = c.Size,
                    MimeType = c.MimeType,
                    IsVirtual = c.IsVirtual,
                    SourceName = string.IsNullOrEmpty(c.SourceName) ? null : c.SourceName,
                    SubFolderPath = string.IsNullOrEmpty(c.SubFolderPath) ? null : c.SubFolderPath,
                })
                .Where(c => c.Id != Guid.Empty)
                .ToList();

            return new MediaScanCandidatesResult
            {
                Success = true,
                TotalFound = response.TotalFound,
                Candidates = candidates,
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning(ex, "Files module is not available for media scan");
            return new MediaScanCandidatesResult
            {
                Success = false,
                ErrorMessage = "Files module is not available.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan media folders via gRPC");
            return new MediaScanCandidatesResult
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    /// <inheritdoc />
    public async Task<ProvisionUserDefaultsResult> ProvisionUserDefaultsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new FilesService.FilesServiceClient(_channel.Value);

            var response = await client.ProvisionUserDefaultsAsync(
                new ProvisionUserDefaultsRequest { UserId = userId.ToString() },
                deadline: DateTime.UtcNow.AddMinutes(11),
                cancellationToken: cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning(
                    "Default-content provisioning failed for {UserId}: {Status} {Error}",
                    userId, response.Status, response.ErrorMessage);
            }

            return new ProvisionUserDefaultsResult(
                response.Success,
                response.Status,
                response.ItemsCreated,
                response.ItemsSkipped,
                response.BytesCopied,
                string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning("Files module is not available for default-content provisioning ({UserId}).", userId);
            return new ProvisionUserDefaultsResult(
                false, "Failed", 0, 0, 0, "Files module is not available.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision default content for {UserId}.", userId);
            return new ProvisionUserDefaultsResult(false, "Failed", 0, 0, 0, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<EnqueueProvisioningResult> EnqueueProvisioningAsync(
        IReadOnlyCollection<Guid> userIds,
        bool allowDeletions = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var queued = 0;
        var skipped = 0;

        try
        {
            var client = new FilesService.FilesServiceClient(_channel.Value);

            // The RPC accepts at most 1000 users per call.
            foreach (var chunk in userIds.Distinct().Chunk(1000))
            {
                var request = new EnqueueProvisioningRequest { AllowDeletions = allowDeletions };
                request.UserIds.AddRange(chunk.Select(id => id.ToString()));

                var response = await client.EnqueueProvisioningAsync(
                    request,
                    deadline: DateTime.UtcNow.Add(_options.Timeout),
                    cancellationToken: cancellationToken);

                queued += response.Queued;
                skipped += response.Skipped;
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning("Files module is not available for default-content convergence enqueue.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue default-content convergence.");
        }

        return new EnqueueProvisioningResult(queued, skipped);
    }

    // This client owns no unmanaged resources: the channel it uses belongs to GrpcChannelManager
    // and is shared with every other core→Files caller, so there is deliberately nothing to dispose.
}
