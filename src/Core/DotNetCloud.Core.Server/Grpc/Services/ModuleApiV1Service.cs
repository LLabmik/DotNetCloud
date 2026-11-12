using Api = DotNetCloud.Core.Grpc.ModuleApi.V1;
using Legacy = DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Services;

/// <summary>
/// Canonical <c>dotnetcloud.moduleapi.v1.ModuleApi</c> service.
/// </summary>
/// <remarks>
/// <para>
/// This is an adapter, not a second implementation: every RPC translates to the existing
/// <see cref="CoreCapabilitiesServiceImpl"/> handler so both surfaces share one code path,
/// one capability grant check (see <c>CapabilityCatalog</c>) and one set of side effects. The
/// legacy messages that carried a caller-supplied identity are left null on purpose — the
/// authenticated transport and the core-signed user assertion are the only identity sources.
/// </para>
/// <para>
/// Differences from the legacy surface are intentional and narrow:
/// <list type="bullet">
/// <item><c>GetUser</c>/<c>SearchUsers</c> return a minimal profile — an email address is only
/// included when the target user consented to the <c>profile</c> scope.</item>
/// <item><c>ReportHealth</c> is new and recorded in <see cref="ModuleHealthRegistry"/>.</item>
/// <item><c>CheckUserConsent</c>/<c>GetGrantedScopes</c> report stored consent intersected with the
/// scopes the module declared, so a module can request exactly what is missing.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class ModuleApiV1Service : Api.ModuleApi.ModuleApiBase
{
    private readonly CoreCapabilitiesServiceImpl _legacy;
    private readonly ModuleHealthRegistry _health;
    private readonly ModuleConsentPolicy _consentPolicy;
    private readonly ModuleFileAccessService _files;
    private readonly ILogger<ModuleApiV1Service> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleApiV1Service"/> class.</summary>
    /// <param name="legacy">The legacy capability handlers every RPC delegates to.</param>
    /// <param name="health">Self-reported module health.</param>
    /// <param name="consentPolicy">Consent policy used to read and narrow stored consent.</param>
    /// <param name="files">Broker for the file capabilities (delegates to the Files module).</param>
    /// <param name="logger">Logger.</param>
    public ModuleApiV1Service(
        CoreCapabilitiesServiceImpl legacy,
        ModuleHealthRegistry health,
        ModuleConsentPolicy consentPolicy,
        ModuleFileAccessService files,
        ILogger<ModuleApiV1Service> logger)
    {
        _legacy = legacy;
        _health = health;
        _consentPolicy = consentPolicy;
        _files = files;
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task<Api.ReportHealthResponse> ReportHealth(
        Api.ReportHealthRequest request,
        ServerCallContext context)
    {
        var moduleId = GetModuleId(context);

        if (!ModuleHealthRegistry.TryNormalizeStatus(request.Status, out var status))
        {
            _logger.LogWarning(
                "ReportHealth from module {ModuleId} used an unknown status; report discarded",
                LogSanitizer.Sanitize(moduleId));

            return Task.FromResult(new Api.ReportHealthResponse { Acknowledged = false });
        }

        _health.Record(moduleId, status, request.Detail, request.ActiveOperations);

        return Task.FromResult(new Api.ReportHealthResponse { Acknowledged = true });
    }

    /// <inheritdoc />
    public override async Task<Api.GetUserResponse> GetUser(
        Api.GetUserRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.GetUser(
            new Legacy.GetUserRequest { UserId = request.UserId },
            context);

        return new Api.GetUserResponse
        {
            Found = legacy.Found,
            User = legacy.User is null
                ? null
                : new Api.ModuleUserProfile
                {
                    Id = legacy.User.Id,
                    DisplayName = legacy.User.DisplayName,
                    AvatarUrl = legacy.User.AvatarUrl,
                    IsActive = legacy.User.IsActive,
                    // Gated by the caller's `profile` consent for this user in the legacy handler.
                    Email = legacy.User.Email,
                },
        };
    }

    /// <inheritdoc />
    public override async Task<Api.SearchUsersResponse> SearchUsers(
        Api.SearchUsersRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.SearchUsers(
            new Legacy.SearchUsersRequest
            {
                Query = request.Query,
                MaxResults = request.MaxResults,
            },
            context);

        var response = new Api.SearchUsersResponse();
        response.Users.AddRange(legacy.Users.Select(user => new Api.ModuleUserProfile
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            IsActive = user.IsActive,
            // Gated by the target user's `profile` consent (see CoreCapabilitiesServiceImpl.SearchUsers).
            Email = user.Email,
        }));

        return response;
    }

    /// <inheritdoc />
    public override async Task<Api.GetCurrentUserContextResponse> GetCurrentUserContext(
        Api.GetCurrentUserContextRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.GetCurrentUser(new Legacy.GetCurrentUserRequest(), context);

        var response = new Api.GetCurrentUserContextResponse
        {
            UserId = legacy.UserId,
            DisplayName = legacy.DisplayName,
            Email = legacy.Email,
            Locale = legacy.Locale,
            Timezone = legacy.Timezone,
        };

        response.Roles.AddRange(legacy.Roles);
        return response;
    }

    /// <inheritdoc />
    public override async Task<Api.GetGroupResponse> GetGroup(
        Api.GetGroupRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.GetGroup(
            new Legacy.GetGroupRequest { GroupId = request.GroupId },
            context);

        return new Api.GetGroupResponse
        {
            Found = legacy.Found,
            Group = legacy.Group is null
                ? null
                : new Api.ModuleGroup
                {
                    Id = legacy.Group.Id,
                    OrganizationId = legacy.Group.OrganizationId,
                    Name = legacy.Group.Name,
                    Description = legacy.Group.Description,
                    IsAllUsersGroup = legacy.Group.IsAllUsersGroup,
                    MemberCount = legacy.Group.MemberCount,
                    CreatedAt = legacy.Group.CreatedAt,
                },
        };
    }

    /// <inheritdoc />
    public override async Task<Api.GetGroupsForUserResponse> GetGroupsForUser(
        Api.GetGroupsForUserRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.GetGroupsForUser(
            new Legacy.GetGroupsForUserRequest { UserId = request.UserId },
            context);

        var response = new Api.GetGroupsForUserResponse();
        response.Groups.AddRange(legacy.Groups.Select(group => new Api.ModuleGroup
        {
            Id = group.Id,
            OrganizationId = group.OrganizationId,
            Name = group.Name,
            Description = group.Description,
            IsAllUsersGroup = group.IsAllUsersGroup,
            MemberCount = group.MemberCount,
            CreatedAt = group.CreatedAt,
        }));

        return response;
    }

    /// <inheritdoc />
    public override async Task<Api.GetTeamResponse> GetTeam(
        Api.GetTeamRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.GetTeam(
            new Legacy.GetTeamRequest { TeamId = request.TeamId },
            context);

        return new Api.GetTeamResponse
        {
            Found = legacy.Found,
            Team = legacy.Team is null
                ? null
                : new Api.ModuleTeam
                {
                    Id = legacy.Team.Id,
                    OrganizationId = legacy.Team.OrganizationId,
                    Name = legacy.Team.Name,
                    Description = legacy.Team.Description,
                    MemberCount = legacy.Team.MemberCount,
                    CreatedAt = legacy.Team.CreatedAt,
                },
        };
    }

    /// <inheritdoc />
    public override async Task<Api.GetTeamsForUserResponse> GetTeamsForUser(
        Api.GetTeamsForUserRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.GetTeamsForUser(
            new Legacy.GetTeamsForUserRequest { UserId = request.UserId },
            context);

        var response = new Api.GetTeamsForUserResponse();
        response.Teams.AddRange(legacy.Teams.Select(team => new Api.ModuleTeam
        {
            Id = team.Id,
            OrganizationId = team.OrganizationId,
            Name = team.Name,
            Description = team.Description,
            MemberCount = team.MemberCount,
            CreatedAt = team.CreatedAt,
        }));

        return response;
    }

    /// <inheritdoc />
    public override async Task<Api.GetUserOrganizationsResponse> GetUserOrganizations(
        Api.GetUserOrganizationsRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.GetUserOrganizations(
            new Legacy.GetUserOrganizationsRequest { UserId = request.UserId },
            context);

        var response = new Api.GetUserOrganizationsResponse();
        response.Organizations.AddRange(legacy.Organizations.Select(organization => new Api.ModuleOrganization
        {
            Id = organization.Id,
            Name = organization.Name,
            Description = organization.Description,
            MemberCount = organization.MemberCount,
            TeamCount = organization.TeamCount,
            CreatedAt = organization.CreatedAt,
        }));

        return response;
    }

    /// <inheritdoc />
    public override async Task<Api.SendNotificationResponse> SendNotification(
        Api.SendNotificationRequest request,
        ServerCallContext context)
    {
        var legacyRequest = new Legacy.SendNotificationRequest
        {
            Title = request.Title,
            Body = request.Body,
            Category = request.Category,
            Link = request.Link,
        };
        legacyRequest.RecipientUserIds.AddRange(request.RecipientUserIds);

        var legacy = await _legacy.SendNotification(legacyRequest, context);

        return new Api.SendNotificationResponse
        {
            Success = legacy.Success,
            DeliveredCount = legacy.DeliveredCount,
        };
    }

    /// <inheritdoc />
    public override async Task<Api.PublishEventResponse> PublishEvent(
        Api.PublishEventRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.PublishEvent(
            new Legacy.PublishEventRequest
            {
                EventType = request.EventType,
                PayloadJson = request.PayloadJson,
            },
            context);

        return new Api.PublishEventResponse
        {
            Success = legacy.Success,
            Error = legacy.Error,
        };
    }

    /// <inheritdoc />
    public override async Task<Api.GetSettingResponse> GetSetting(
        Api.GetSettingRequest request,
        ServerCallContext context)
    {
        // The legacy handler already derives the namespace from the authenticated module id.
        var legacy = await _legacy.GetSetting(
            new Legacy.GetSettingRequest { Key = request.Key },
            context);

        return new Api.GetSettingResponse
        {
            Found = legacy.Found,
            Value = legacy.Value,
        };
    }

    /// <inheritdoc />
    public override async Task<Api.SetSettingResponse> SetSetting(
        Api.SetSettingRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.SetSetting(
            new Legacy.SetSettingRequest
            {
                Key = request.Key,
                Value = request.Value,
            },
            context);

        return new Api.SetSettingResponse { Success = legacy.Success };
    }

    /// <inheritdoc />
    public override async Task<Api.LogAuditResponse> LogAudit(
        Api.LogAuditRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.LogAudit(
            new Legacy.LogAuditRequest
            {
                Action = request.Action,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                Description = request.Description,
            },
            context);

        return new Api.LogAuditResponse { Success = legacy.Success };
    }

    /// <inheritdoc />
    public override async Task<Api.SubmitSearchIndexResponse> SubmitSearchIndex(
        Api.SubmitSearchIndexRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.SubmitSearchIndex(
            new Legacy.SubmitSearchIndexRequest
            {
                EntityId = request.EntityId,
                Action = request.Action,
            },
            context);

        return new Api.SubmitSearchIndexResponse { Success = legacy.Success };
    }

    /// <inheritdoc />
    public override async Task<Api.BroadcastRealtimeEventResponse> BroadcastRealtimeEvent(
        Api.BroadcastRealtimeEventRequest request,
        ServerCallContext context)
    {
        var legacy = await _legacy.BroadcastRealtimeEvent(
            new Legacy.BroadcastRealtimeEventRequest
            {
                Group = request.Group,
                EventName = request.EventName,
                PayloadJson = request.PayloadJson,
                TargetUserId = request.TargetUserId,
            },
            context);

        return new Api.BroadcastRealtimeEventResponse { Success = legacy.Success };
    }

    /// <inheritdoc />
    public override async Task<Api.CheckUserConsentResponse> CheckUserConsent(
        Api.CheckUserConsentRequest request,
        ServerCallContext context)
    {
        var moduleId = GetModuleId(context);
        var userId = ResolveConsentSubject(context, request.UserId);

        var requested = request.Scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var granted = await GetEffectiveScopesAsync(moduleId, userId, context.CancellationToken);
        var grantedSet = new HashSet<string>(granted, StringComparer.Ordinal);

        var response = new Api.CheckUserConsentResponse
        {
            Granted = requested.All(grantedSet.Contains),
        };

        response.MissingScopes.AddRange(requested.Where(scope => !grantedSet.Contains(scope)));

        return response;
    }

    /// <inheritdoc />
    public override async Task<Api.GetGrantedScopesResponse> GetGrantedScopes(
        Api.GetGrantedScopesRequest request,
        ServerCallContext context)
    {
        var moduleId = GetModuleId(context);
        var userId = ResolveConsentSubject(context, request.UserId);

        var response = new Api.GetGrantedScopesResponse();
        response.Scopes.AddRange(await GetEffectiveScopesAsync(moduleId, userId, context.CancellationToken));

        return response;
    }

    // --- Files (IFileDirectory / IFileWriter) ----------------------------------------------------

    /// <inheritdoc />
    public override async Task<Api.ListFilesResponse> ListFiles(
        Api.ListFilesRequest request,
        ServerCallContext context)
    {
        var userId = RequireActingUser(context);

        var result = await _files.ListAsync(
            GetModuleId(context),
            userId,
            ParseOptionalGuid(request.ParentId),
            request.Page,
            request.PageSize,
            context.CancellationToken);

        var response = new Api.ListFilesResponse { TotalCount = result.TotalCount };
        response.Nodes.AddRange(result.Nodes.Select(ToModuleFileNode));

        return response;
    }

    /// <inheritdoc />
    public override async Task DownloadFile(
        Api.DownloadFileRequest request,
        IServerStreamWriter<Api.FileContentChunk> responseStream,
        ServerCallContext context)
    {
        var userId = RequireActingUser(context);

        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "node_id must be a GUID."));
        }

        await using var content = (await _files.OpenReadAsync(
            GetModuleId(context),
            userId,
            nodeId,
            context.CancellationToken)).Content;

        if (content is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "The file has no readable content."));
        }

        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await content.ReadAsync(buffer, context.CancellationToken)) > 0)
        {
            await responseStream.WriteAsync(
                new Api.FileContentChunk { Data = Google.Protobuf.ByteString.CopyFrom(buffer, 0, read) },
                context.CancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task<Api.UploadFileResponse> UploadFile(
        IAsyncStreamReader<Api.UploadFileRequest> requestStream,
        ServerCallContext context)
    {
        var userId = RequireActingUser(context);
        var moduleId = GetModuleId(context);

        Api.ModuleFileUploadMetadata? metadata = null;
        long received = 0;

        // The stream is spooled to a temp file: chunks arrive incrementally and the Files client
        // reads the content twice (hash pass, then upload), which a network stream cannot support.
        var path = Path.Combine(Path.GetTempPath(), $"dnc-module-upload-{Guid.CreateVersion7():N}.tmp");
        await using var spool = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
        {
            switch (message.PayloadCase)
            {
                case Api.UploadFileRequest.PayloadOneofCase.Metadata:
                    if (metadata is not null)
                    {
                        throw new RpcException(new Status(
                            StatusCode.InvalidArgument,
                            "Upload metadata may only be sent once, as the first message."));
                    }

                    metadata = message.Metadata;

                    if (metadata.TotalSize > ModuleFileAccessService.MaxUploadBytes)
                    {
                        throw new RpcException(new Status(
                            StatusCode.ResourceExhausted,
                            $"A module may store at most {ModuleFileAccessService.MaxUploadBytes} bytes in one call."));
                    }

                    break;

                case Api.UploadFileRequest.PayloadOneofCase.Chunk:
                    if (metadata is null)
                    {
                        throw new RpcException(new Status(
                            StatusCode.InvalidArgument,
                            "The first upload message must carry metadata."));
                    }

                    received += message.Chunk.Data.Length;

                    if (received > ModuleFileAccessService.MaxUploadBytes)
                    {
                        throw new RpcException(new Status(
                            StatusCode.ResourceExhausted,
                            $"A module may store at most {ModuleFileAccessService.MaxUploadBytes} bytes in one call."));
                    }

                    await spool.WriteAsync(message.Chunk.Data.Memory, context.CancellationToken);
                    break;

                default:
                    throw new RpcException(new Status(
                        StatusCode.InvalidArgument,
                        "An upload message must carry metadata or chunk data."));
            }
        }

        if (metadata is null)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "The upload stream carried no metadata message."));
        }

        spool.Position = 0;

        var saved = await _files.SaveAsync(
            moduleId,
            userId,
            ParseOptionalGuid(metadata.ParentId),
            metadata.FileName,
            metadata.MimeType,
            spool,
            context.CancellationToken);

        if (saved.Node is null)
        {
            throw new RpcException(new Status(
                StatusCode.Internal,
                "The Files module did not return the stored node."));
        }

        return new Api.UploadFileResponse { Node = ToModuleFileNode(saved.Node) };
    }

    /// <summary>
    /// Requires the call to act for a verified end user.
    /// </summary>
    /// <param name="context">The gRPC call context.</param>
    /// <returns>The acting user id.</returns>
    /// <remarks>
    /// File operations have no module-scope form: a module uploading "for nobody" would either have
    /// to name a user itself (which the core never accepts) or write into a shared namespace. Both
    /// are refused instead of guessed at.
    /// </remarks>
    private static Guid RequireActingUser(ServerCallContext context)
    {
        var binding = CallerSubjectGuard.Bind(context, requestedUserId: null);

        if (!binding.IsAllowed || binding.IsModuleScope || binding.UserId == Guid.Empty)
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "File access requires a verified end-user assertion."));
        }

        return binding.UserId;
    }

    /// <summary>Maps a capability-level file node onto the wire shape.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The proto node.</returns>
    private static Api.ModuleFileNode ToModuleFileNode(FileNodeInfo node) =>
        new()
        {
            Id = node.Id.ToString(),
            Name = node.Name,
            NodeType = node.NodeType,
            MimeType = node.MimeType ?? string.Empty,
            Size = node.Size,
            ParentId = node.ParentId?.ToString() ?? string.Empty,
            UpdatedAt = node.UpdatedAt?.ToString("O") ?? string.Empty,
        };

    /// <summary>Parses an optional GUID field, where empty means "not set".</summary>
    /// <param name="value">The wire value.</param>
    /// <returns>The GUID, or <see langword="null"/>.</returns>
    private static Guid? ParseOptionalGuid(string? value) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty ? parsed : null;

    /// <summary>
    /// Resolves the user a consent query is about, binding it to the verified caller identity.
    /// </summary>
    /// <param name="context">The gRPC call context.</param>
    /// <param name="requestedUserId">The payload user id; empty means the acting user.</param>
    /// <returns>The user id, or <see cref="Guid.Empty"/> when the call carries no user subject.</returns>
    private static Guid ResolveConsentSubject(ServerCallContext context, string? requestedUserId)
    {
        var binding = CallerSubjectGuard.Bind(context, requestedUserId);
        if (!binding.IsAllowed)
        {
            throw CallerSubjectGuard.CreateDeniedException(binding.Outcome);
        }

        return binding.UserId;
    }

    /// <summary>
    /// Gets the scopes the module may actually use for a user: stored consent intersected with the
    /// scopes the module declared. A scope the manifest does not declare can never be granted.
    /// </summary>
    /// <param name="moduleId">The authenticated module id.</param>
    /// <param name="userId">The user id, or <see cref="Guid.Empty"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective scopes, in catalog order.</returns>
    private async Task<IReadOnlyList<string>> GetEffectiveScopesAsync(
        string moduleId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var granted = await _consentPolicy.GetGrantedScopesAsync(moduleId, userId, cancellationToken);
        var declared = _consentPolicy.GetDeclaredScopes(moduleId);

        return declared is null
            ? granted
            : granted.Where(scope => declared.Contains(scope, StringComparer.Ordinal)).ToArray();
    }

    /// <summary>
    /// Gets the verified module id. <c>AuthenticationInterceptor</c> runs before this service, so
    /// a missing value means the interceptor chain is misconfigured and the call fails closed.
    /// </summary>
    private static string GetModuleId(ServerCallContext context)
    {
        if (context.UserState.TryGetValue("ModuleId", out var value) && value is string moduleId
            && !string.IsNullOrWhiteSpace(moduleId))
        {
            return moduleId;
        }

        throw new RpcException(new Status(
            StatusCode.Unauthenticated,
            "No authenticated module identity is available for this call"));
    }
}
