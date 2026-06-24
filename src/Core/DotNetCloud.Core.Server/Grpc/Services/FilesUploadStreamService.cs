using DotNetCloud.Modules.Files.Host.Protos;
using DotNetCloud.Core.Server.Grpc.Clients;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;

namespace DotNetCloud.Core.Server.Grpc.Services;

/// <summary>
/// gRPC service that exposes FilesService to desktop/mobile clients.
/// Delegates upload streaming to the Files module host via gRPC unary calls.
/// </summary>
public sealed class FilesUploadStreamService : FilesService.FilesServiceBase
{
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<FilesUploadStreamService> _logger;

    public FilesUploadStreamService(
        ModuleEndpointProvider endpointProvider,
        ILogger<FilesUploadStreamService> logger)
    {
        _endpointProvider = endpointProvider;
        _logger = logger;
    }

    public override async Task<UploadFileStreamResponse> UploadFileStream(
        IAsyncStreamReader<UploadFileStreamRequest> requestStream,
        ServerCallContext context)
    {
        InitiateUploadResponse? session = null;
        InitiateUploadRequest? metadata = null;
        FilesService.FilesServiceClient? client = null;
        GrpcChannel? channel = null;

        try
        {
            var address = _endpointProvider.GetEndpoint("dotnetcloud.files");
            channel = GrpcChannel.ForAddress(
                address,
                new GrpcChannelOptions
                {
                    UnsafeUseInsecureChannelCallCredentials = true,
                    HttpHandler = new SocketsHttpHandler
                    {
                        EnableMultipleHttp2Connections = true,
                        ConnectTimeout = TimeSpan.FromSeconds(5)
                    }
                });
            client = new FilesService.FilesServiceClient(channel);

            await foreach (var msg in requestStream.ReadAllAsync(context.CancellationToken))
            {
                switch (msg.PayloadCase)
                {
                    case UploadFileStreamRequest.PayloadOneofCase.Metadata:
                        if (session is not null)
                        {
                            return new UploadFileStreamResponse
                            {
                                Success = false,
                                ErrorMessage = "Metadata already received. Stream must start with metadata exactly once."
                            };
                        }

                        metadata = msg.Metadata;

                        // Extract user ID from JWT in gRPC auth header.
                        var userId = GetUserIdFromContext(context);
                        if (userId is null)
                        {
                            return new UploadFileStreamResponse
                            {
                                Success = false,
                                ErrorMessage = "Authentication required."
                            };
                        }
                        metadata.UserId = userId;

                        session = await client.InitiateUploadAsync(
                            metadata,
                            deadline: DateTime.UtcNow.Add(TimeSpan.FromSeconds(30)),
                            cancellationToken: context.CancellationToken);

                        if (!session.Success)
                        {
                            return new UploadFileStreamResponse
                            {
                                Success = false,
                                ErrorMessage = session.ErrorMessage
                            };
                        }

                        _logger.LogInformation(
                            "UploadFileStream session {SessionId} initiated for {FileName}",
                            session.SessionId, metadata.FileName);
                        break;

                    case UploadFileStreamRequest.PayloadOneofCase.Chunk:
                        if (session is null)
                        {
                            return new UploadFileStreamResponse
                            {
                                Success = false,
                                ErrorMessage = "Chunk received before metadata. Send metadata first."
                            };
                        }

                        var chunkReq = msg.Chunk;
                        chunkReq.SessionId = session.SessionId;

                        var chunkResp = await client.UploadChunkAsync(
                            chunkReq,
                            deadline: DateTime.UtcNow.Add(TimeSpan.FromSeconds(30)),
                            cancellationToken: context.CancellationToken);

                        if (!chunkResp.Success)
                        {
                            return new UploadFileStreamResponse
                            {
                                Success = false,
                                ErrorMessage = chunkResp.ErrorMessage
                            };
                        }
                        break;

                    default:
                        return new UploadFileStreamResponse
                        {
                            Success = false,
                            ErrorMessage = "Unknown payload type in stream."
                        };
                }
            }

            if (session is null || metadata is null)
            {
                return new UploadFileStreamResponse
                {
                    Success = false,
                    ErrorMessage = "No metadata received before stream ended."
                };
            }

            var completeResp = await client.CompleteUploadAsync(
                new CompleteUploadRequest
                {
                    SessionId = session.SessionId,
                    UserId = metadata.UserId
                },
                deadline: DateTime.UtcNow.Add(TimeSpan.FromMinutes(5)),
                cancellationToken: context.CancellationToken);

            if (!completeResp.Success)
            {
                return new UploadFileStreamResponse
                {
                    Success = false,
                    ErrorMessage = completeResp.ErrorMessage,
                    SessionId = session.SessionId
                };
            }

            _logger.LogInformation(
                "UploadFileStream session {SessionId} completed. Node {NodeId}",
                session.SessionId, completeResp.Node.Id);

            return new UploadFileStreamResponse
            {
                Success = true,
                Node = completeResp.Node,
                SessionId = session.SessionId
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "UploadFileStream failed");
            return new UploadFileStreamResponse
            {
                Success = false,
                ErrorMessage = $"Upload failed: {ex.Message}"
            };
        }
        finally
        {
            channel?.Dispose();
        }
    }

    // Unimplemented RPCs return Unavailable so clients don't call them through this proxy.
    // All other FilesService RPCs go through YARP REST API (unchanged).

    private static readonly Status UnavailableStatus = new(StatusCode.Unavailable, "Use REST API or dedicated gRPC endpoint.");

    public override Task<CreateFolderResponse> CreateFolder(CreateFolderRequest request, ServerCallContext context)
        => ThrowUnavailable<CreateFolderResponse>();

    public override Task<ListNodesResponse> ListNodes(ListNodesRequest request, ServerCallContext context)
        => ThrowUnavailable<ListNodesResponse>();

    public override Task<GetNodeResponse> GetNode(GetNodeRequest request, ServerCallContext context)
        => ThrowUnavailable<GetNodeResponse>();

    public override Task<RenameNodeResponse> RenameNode(RenameNodeRequest request, ServerCallContext context)
        => ThrowUnavailable<RenameNodeResponse>();

    public override Task<MoveNodeResponse> MoveNode(MoveNodeRequest request, ServerCallContext context)
        => ThrowUnavailable<MoveNodeResponse>();

    public override Task<CopyNodeResponse> CopyNode(CopyNodeRequest request, ServerCallContext context)
        => ThrowUnavailable<CopyNodeResponse>();

    public override Task<DeleteNodeResponse> DeleteNode(DeleteNodeRequest request, ServerCallContext context)
        => ThrowUnavailable<DeleteNodeResponse>();

    public override Task<ListTrashResponse> ListTrash(ListTrashRequest request, ServerCallContext context)
        => ThrowUnavailable<ListTrashResponse>();

    public override Task<RestoreNodeResponse> RestoreNode(RestoreNodeRequest request, ServerCallContext context)
        => ThrowUnavailable<RestoreNodeResponse>();

    public override Task<PurgeNodeResponse> PurgeNode(PurgeNodeRequest request, ServerCallContext context)
        => ThrowUnavailable<PurgeNodeResponse>();

    public override Task<EmptyTrashResponse> EmptyTrash(EmptyTrashRequest request, ServerCallContext context)
        => ThrowUnavailable<EmptyTrashResponse>();

    public override Task<InitiateUploadResponse> InitiateUpload(InitiateUploadRequest request, ServerCallContext context)
        => ThrowUnavailable<InitiateUploadResponse>();

    public override Task<UploadChunkResponse> UploadChunk(UploadChunkRequest request, ServerCallContext context)
        => ThrowUnavailable<UploadChunkResponse>();

    public override Task<CompleteUploadResponse> CompleteUpload(CompleteUploadRequest request, ServerCallContext context)
        => ThrowUnavailable<CompleteUploadResponse>();

    public override Task DownloadFile(DownloadFileRequest request, IServerStreamWriter<DownloadFileResponse> responseStream, ServerCallContext context)
        => throw new RpcException(UnavailableStatus);

    public override Task<ListVersionsResponse> ListVersions(ListVersionsRequest request, ServerCallContext context)
        => ThrowUnavailable<ListVersionsResponse>();

    public override Task<RestoreVersionResponse> RestoreVersion(RestoreVersionRequest request, ServerCallContext context)
        => ThrowUnavailable<RestoreVersionResponse>();

    public override Task<CreateShareResponse> CreateShare(CreateShareRequest request, ServerCallContext context)
        => ThrowUnavailable<CreateShareResponse>();

    public override Task<ListSharesResponse> ListShares(ListSharesRequest request, ServerCallContext context)
        => ThrowUnavailable<ListSharesResponse>();

    public override Task<RevokeShareResponse> RevokeShare(RevokeShareRequest request, ServerCallContext context)
        => ThrowUnavailable<RevokeShareResponse>();

    public override Task<GetQuotaResponse> GetQuota(GetQuotaRequest request, ServerCallContext context)
        => ThrowUnavailable<GetQuotaResponse>();

    public override Task<ToggleFavoriteResponse> ToggleFavorite(ToggleFavoriteRequest request, ServerCallContext context)
        => ThrowUnavailable<ToggleFavoriteResponse>();

    public override Task GetSearchableDocuments(GetSearchableDocumentsRequest request, IServerStreamWriter<SearchableDocument> responseStream, ServerCallContext context)
        => throw new RpcException(UnavailableStatus);

    public override Task<SearchableDocumentResponse> GetSearchableDocument(GetSearchableDocumentRequest request, ServerCallContext context)
        => ThrowUnavailable<SearchableDocumentResponse>();

    public override Task<ScanMediaFoldersResponse> ScanMediaFolders(ScanMediaFoldersRequest request, ServerCallContext context)
        => ThrowUnavailable<ScanMediaFoldersResponse>();

    private static Task<T> ThrowUnavailable<T>()
    {
        throw new RpcException(UnavailableStatus);
    }

    private static string? GetUserIdFromContext(ServerCallContext context)
    {
        foreach (var entry in context.RequestHeaders)
        {
            if (string.Equals(entry.Key, "authorization", StringComparison.OrdinalIgnoreCase) &&
                entry.Value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = entry.Value["Bearer ".Length..];
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(token))
                    {
                        var jwt = handler.ReadJwtToken(token);
                        return jwt.Subject;
                    }
                }
                catch
                {
                }
            }
        }
        return null;
    }
}
