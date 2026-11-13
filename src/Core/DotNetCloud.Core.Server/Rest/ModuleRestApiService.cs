using System.Diagnostics;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Server.Grpc.Interceptors;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.Grpc.Services;
using Grpc.Core;
using Api = DotNetCloud.Core.Grpc.ModuleApi.V1;

namespace DotNetCloud.Core.Server.Rest;

/// <summary>
/// Canonical gRPC method paths the REST facade maps onto, so both surfaces share one capability
/// catalog entry, one rate-limit category and one audit trail.
/// </summary>
internal static class ModuleRestMethods
{
    private const string Service = "/dotnetcloud.moduleapi.v1.ModuleApi/";

    /// <summary><c>GET /me</c>.</summary>
    public const string GetCurrentUserContext = Service + "GetCurrentUserContext";

    /// <summary><c>GET /users/{userId}</c>.</summary>
    public const string GetUser = Service + "GetUser";

    /// <summary><c>GET /users?query=</c>.</summary>
    public const string SearchUsers = Service + "SearchUsers";

    /// <summary><c>GET /groups/{groupId}</c>.</summary>
    public const string GetGroup = Service + "GetGroup";

    /// <summary><c>GET /users/{userId}/groups</c>.</summary>
    public const string GetGroupsForUser = Service + "GetGroupsForUser";

    /// <summary><c>GET /teams/{teamId}</c>.</summary>
    public const string GetTeam = Service + "GetTeam";

    /// <summary><c>GET /users/{userId}/teams</c>.</summary>
    public const string GetTeamsForUser = Service + "GetTeamsForUser";

    /// <summary><c>POST /notifications</c>.</summary>
    public const string SendNotification = Service + "SendNotification";

    /// <summary><c>POST /events</c>.</summary>
    public const string PublishEvent = Service + "PublishEvent";

    /// <summary><c>POST /search/index</c>.</summary>
    public const string SubmitSearchIndex = Service + "SubmitSearchIndex";

    /// <summary><c>POST /realtime/broadcast</c>.</summary>
    public const string BroadcastRealtimeEvent = Service + "BroadcastRealtimeEvent";

    /// <summary><c>POST /audit</c>.</summary>
    public const string LogAudit = Service + "LogAudit";

    /// <summary><c>GET /settings/{key}</c>.</summary>
    public const string GetSetting = Service + "GetSetting";

    /// <summary><c>PUT /settings/{key}</c>.</summary>
    public const string SetSetting = Service + "SetSetting";

    /// <summary><c>GET /consent/{userId}</c>.</summary>
    public const string CheckUserConsent = Service + "CheckUserConsent";

    /// <summary><c>GET /consent/scopes</c>.</summary>
    public const string GetGrantedScopes = Service + "GetGrantedScopes";

    /// <summary><c>GET /files</c>.</summary>
    public const string ListFiles = Service + "ListFiles";

    /// <summary><c>GET /files/{nodeId}/content</c>.</summary>
    public const string DownloadFile = Service + "DownloadFile";

    /// <summary><c>POST /files</c>.</summary>
    public const string UploadFile = Service + "UploadFile";

    /// <summary><c>POST /health</c>.</summary>
    public const string ReportHealth = Service + "ReportHealth";
}

/// <summary>
/// Implements the internal module REST facade: JSON over the module credential and user assertion,
/// evaluated by the same interceptors that guard the gRPC listener.
/// </summary>
/// <remarks>
/// <para>
/// This class holds no authorization policy of its own. Each request is wrapped in a
/// <see cref="ModuleHttpServerCallContext"/> and pushed through
/// <c>AuthenticationInterceptor</c> → <c>CallerContextInterceptor</c> →
/// <c>ModuleRateLimitInterceptor</c> → <c>ModuleAccessInterceptor</c>, then handed to the canonical
/// <see cref="ModuleApiV1Service"/>. A rule added to any of those interceptors therefore applies to
/// both transports at once.
/// </para>
/// <para>
/// Failures are returned as <see cref="ApiErrorResponse"/> with the error code from the gRPC
/// <c>error-code</c> trailer when present, so a REST caller sees the same stable code a gRPC caller
/// would (for example <c>CAP_NOT_GRANTED</c> or <c>MODULE_RATE_LIMITED</c>).
/// </para>
/// </remarks>
internal sealed class ModuleRestApiService
{
    private readonly ModuleApiV1Service _api;
    private readonly AuthenticationInterceptor _authentication;
    private readonly CallerContextInterceptor _callerContext;
    private readonly ModuleRateLimitInterceptor _rateLimit;
    private readonly ModuleAccessInterceptor _access;
    private readonly Services.ModuleFileAccessService _files;

    /// <summary>Initializes a new instance of the <see cref="ModuleRestApiService"/> class.</summary>
    /// <param name="api">The canonical module API implementation.</param>
    /// <param name="authentication">The transport credential check.</param>
    /// <param name="callerContext">The caller-context (user assertion) check.</param>
    /// <param name="rateLimit">The per-module rate limiter.</param>
    /// <param name="access">The capability evaluator adapter.</param>
    /// <param name="files">Broker for the file capabilities.</param>
    public ModuleRestApiService(
        ModuleApiV1Service api,
        AuthenticationInterceptor authentication,
        CallerContextInterceptor callerContext,
        ModuleRateLimitInterceptor rateLimit,
        ModuleAccessInterceptor access,
        Services.ModuleFileAccessService files)
    {
        _api = api;
        _authentication = authentication;
        _callerContext = callerContext;
        _rateLimit = rateLimit;
        _access = access;
        _files = files;
    }

    /// <summary>Gets the authenticated module id for a request that already passed the pipeline.</summary>
    /// <param name="context">A context that has completed the pipeline.</param>
    /// <returns>The verified module id.</returns>
    public static string GetModuleId(ServerCallContext context) =>
        context.UserState.TryGetValue("ModuleId", out var value) && value is string moduleId
            ? moduleId
            : "unknown";

    /// <summary>Handles <c>GET /me</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <returns>The response.</returns>
    public Task<IResult> GetMeAsync(HttpContext http) =>
        ExecuteAsync(http, ModuleRestMethods.GetCurrentUserContext, async context =>
        {
            var response = await _api.GetCurrentUserContext(new Api.GetCurrentUserContextRequest(), context);

            return Json(new
            {
                userId = response.UserId,
                displayName = response.DisplayName,
                email = response.Email,
                locale = response.Locale,
                timezone = response.Timezone,
                roles = response.Roles,
            });
        });

    /// <summary>Handles <c>GET /users/{userId}</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="userId">The user to look up.</param>
    /// <returns>The response.</returns>
    public Task<IResult> GetUserAsync(HttpContext http, string userId) =>
        ExecuteAsync(http, ModuleRestMethods.GetUser, async context =>
        {
            var response = await _api.GetUser(new Api.GetUserRequest { UserId = userId }, context);

            return Json(new
            {
                found = response.Found,
                user = response.User is null ? null : ToProfile(response.User),
            });
        });

    /// <summary>Handles <c>GET /users?query=</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="query">The search term.</param>
    /// <param name="limit">Maximum results (defaults to 20, capped at 50).</param>
    /// <returns>The response.</returns>
    public Task<IResult> SearchUsersAsync(HttpContext http, string? query, int? limit) =>
        ExecuteAsync(http, ModuleRestMethods.SearchUsers, async context =>
        {
            var response = await _api.SearchUsers(
                new Api.SearchUsersRequest
                {
                    Query = query ?? string.Empty,
                    MaxResults = Math.Clamp(limit ?? 20, 1, 50),
                },
                context);

            return Json(new { users = response.Users.Select(ToProfile).ToArray() });
        });

    /// <summary>Handles <c>GET /groups/{groupId}</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="groupId">The group to look up.</param>
    /// <returns>The response.</returns>
    public Task<IResult> GetGroupAsync(HttpContext http, string groupId) =>
        ExecuteAsync(http, ModuleRestMethods.GetGroup, async context =>
        {
            var response = await _api.GetGroup(new Api.GetGroupRequest { GroupId = groupId }, context);

            return Json(new
            {
                found = response.Found,
                group = response.Group is null ? null : ToGroup(response.Group),
            });
        });

    /// <summary>Handles <c>GET /users/{userId}/groups</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="userId">The user whose groups are requested.</param>
    /// <returns>The response.</returns>
    public Task<IResult> GetGroupsForUserAsync(HttpContext http, string userId) =>
        ExecuteAsync(http, ModuleRestMethods.GetGroupsForUser, async context =>
        {
            var response = await _api.GetGroupsForUser(
                new Api.GetGroupsForUserRequest { UserId = userId },
                context);

            return Json(new { groups = response.Groups.Select(ToGroup).ToArray() });
        });

    /// <summary>Handles <c>GET /teams/{teamId}</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="teamId">The team to look up.</param>
    /// <returns>The response.</returns>
    public Task<IResult> GetTeamAsync(HttpContext http, string teamId) =>
        ExecuteAsync(http, ModuleRestMethods.GetTeam, async context =>
        {
            var response = await _api.GetTeam(new Api.GetTeamRequest { TeamId = teamId }, context);

            return Json(new
            {
                found = response.Found,
                team = response.Team is null ? null : ToTeam(response.Team),
            });
        });

    /// <summary>Handles <c>GET /users/{userId}/teams</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="userId">The user whose teams are requested.</param>
    /// <returns>The response.</returns>
    public Task<IResult> GetTeamsForUserAsync(HttpContext http, string userId) =>
        ExecuteAsync(http, ModuleRestMethods.GetTeamsForUser, async context =>
        {
            var response = await _api.GetTeamsForUser(
                new Api.GetTeamsForUserRequest { UserId = userId },
                context);

            return Json(new { teams = response.Teams.Select(ToTeam).ToArray() });
        });

    /// <summary>Handles <c>POST /notifications</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="request">The notification to send.</param>
    /// <returns>The response.</returns>
    public Task<IResult> SendNotificationAsync(HttpContext http, ModuleRestContracts.NotificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteAsync(http, ModuleRestMethods.SendNotification, async context =>
        {
            var response = await _api.SendNotification(request.ToProto(), context);

            return Json(new { success = response.Success, deliveredCount = response.DeliveredCount });
        });
    }

    /// <summary>Handles <c>POST /events</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="request">The event to publish.</param>
    /// <returns>The response.</returns>
    public Task<IResult> PublishEventAsync(HttpContext http, ModuleRestContracts.EventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteAsync(http, ModuleRestMethods.PublishEvent, async context =>
        {
            var response = await _api.PublishEvent(request.ToProto(), context);
            var status = response.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest;

            return Results.Json(new { success = response.Success, error = response.Error }, statusCode: status);
        });
    }

    /// <summary>Handles <c>POST /search/index</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="request">The index request.</param>
    /// <returns>The response.</returns>
    public Task<IResult> SubmitSearchIndexAsync(HttpContext http, ModuleRestContracts.SearchIndexRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteAsync(http, ModuleRestMethods.SubmitSearchIndex, async context =>
        {
            var response = await _api.SubmitSearchIndex(request.ToProto(), context);

            return Json(new { success = response.Success });
        });
    }

    /// <summary>Handles <c>POST /realtime/broadcast</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="request">The broadcast.</param>
    /// <returns>The response.</returns>
    public Task<IResult> BroadcastRealtimeEventAsync(
        HttpContext http,
        ModuleRestContracts.RealtimeBroadcastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteAsync(http, ModuleRestMethods.BroadcastRealtimeEvent, async context =>
        {
            var response = await _api.BroadcastRealtimeEvent(request.ToProto(), context);

            return Json(new { success = response.Success });
        });
    }

    /// <summary>Handles <c>POST /audit</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="request">The audit entry.</param>
    /// <returns>The response.</returns>
    public Task<IResult> LogAuditAsync(HttpContext http, ModuleRestContracts.AuditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteAsync(http, ModuleRestMethods.LogAudit, async context =>
        {
            var response = await _api.LogAudit(request.ToProto(), context);

            return Json(new { success = response.Success });
        });
    }

    /// <summary>Handles <c>GET /settings/{key}</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="key">The setting key.</param>
    /// <returns>The response.</returns>
    public Task<IResult> GetSettingAsync(HttpContext http, string key) =>
        ExecuteAsync(http, ModuleRestMethods.GetSetting, async context =>
        {
            var response = await _api.GetSetting(new Api.GetSettingRequest { Key = key }, context);

            return Json(new { found = response.Found, value = response.Value });
        });

    /// <summary>Handles <c>PUT /settings/{key}</c>.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="request">The value to store.</param>
    /// <returns>The response.</returns>
    public Task<IResult> SetSettingAsync(
        HttpContext http,
        string key,
        ModuleRestContracts.SettingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteAsync(http, ModuleRestMethods.SetSetting, async context =>
        {
            var canonical = request.ToProto();
            canonical.Key = key;

            var response = await _api.SetSetting(canonical, context);

            return Json(new { success = response.Success });
        });
    }

    /// <summary>Handles <c>GET /consent/{userId}</c> (answers 501 until phase A3).</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="userId">The user to check.</param>
    /// <returns>The response.</returns>
    public Task<IResult> CheckUserConsentAsync(HttpContext http, string userId) =>
        ExecuteAsync(http, ModuleRestMethods.CheckUserConsent, async context =>
        {
            var canonical = new Api.CheckUserConsentRequest { UserId = userId };
            var response = await _api.CheckUserConsent(canonical, context);

            return Json(new { granted = response.Granted, missingScopes = response.MissingScopes });
        });

    /// <summary>Handles <c>GET /consent/scopes</c> (answers 501 until phase A3).</summary>
    /// <param name="http">The HTTP request.</param>
    /// <returns>The response.</returns>
    public Task<IResult> GetGrantedScopesAsync(HttpContext http) =>
        ExecuteAsync(http, ModuleRestMethods.GetGrantedScopes, async context =>
        {
            var response = await _api.GetGrantedScopes(new Api.GetGrantedScopesRequest(), context);

            return Json(new { scopes = response.Scopes });
        });

    // --- Files (IFileDirectory / IFileWriter) ----------------------------------------------------

    /// <summary>Handles <c>GET /files?parentId=</c>: lists one folder of the acting user's files.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="parentId">Folder to list; empty or absent lists the user's root.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Requested page size (the core caps it).</param>
    /// <returns>The folder listing.</returns>
    public Task<IResult> ListFilesAsync(HttpContext http, string? parentId, int? page, int? pageSize) =>
        ExecuteAsync(http, ModuleRestMethods.ListFiles, async context =>
        {
            var response = await _api.ListFiles(
                new Api.ListFilesRequest
                {
                    ParentId = parentId ?? string.Empty,
                    Page = page ?? 1,
                    PageSize = pageSize ?? 200,
                },
                context);

            return Json(new
            {
                nodes = response.Nodes.Select(ToFileNode).ToArray(),
                totalCount = response.TotalCount,
            });
        });

    /// <summary>Handles <c>GET /files/{nodeId}/content</c>: streams one file's current content.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <param name="nodeId">The file node.</param>
    /// <returns>The content stream.</returns>
    /// <remarks>
    /// The facade streams straight from the Files module's spooled content. The MIME type is not
    /// echoed: the caller already listed the folder and knows which node it asked for, and a wrong
    /// content type is worse than a generic one for a byte-exact copy.
    /// </remarks>
    public Task<IResult> DownloadFileAsync(HttpContext http, string nodeId) =>
        ExecuteAsync(http, ModuleRestMethods.DownloadFile, async context =>
        {
            var (moduleId, userId) = RequireActingUser(context);

            if (!Guid.TryParse(nodeId, out var id))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "nodeId must be a GUID."));
            }

            var read = await _files.OpenReadAsync(moduleId, userId, id, context.CancellationToken);

            return Results.Stream(read.Content!, "application/octet-stream");
        });

    /// <summary>Handles <c>POST /files?parentId=&amp;fileName=</c>: stores a file in the user's files.</summary>
    /// <param name="http">The HTTP request; its body is the file content.</param>
    /// <param name="parentId">Target folder; empty stores in the user's root.</param>
    /// <param name="fileName">File name.</param>
    /// <param name="contentType">MIME type.</param>
    /// <returns>The stored node.</returns>
    public Task<IResult> UploadFileAsync(
        HttpContext http,
        string? parentId,
        string fileName,
        string? contentType) =>
        ExecuteAsync(http, ModuleRestMethods.UploadFile, async context =>
        {
            var (moduleId, userId) = RequireActingUser(context);

            var saved = await _files.SaveAsync(
                moduleId,
                userId,
                Guid.TryParse(parentId, out var folderId) && folderId != Guid.Empty ? folderId : null,
                fileName,
                contentType ?? "application/octet-stream",
                http.Request.Body,
                context.CancellationToken);

            return Json(new { node = ToFileNode(saved.Node!) });
        });

    /// <summary>Handles <c>GET /health</c>: proves the credential and returns the module identity.</summary>
    /// <param name="http">The HTTP request.</param>
    /// <returns>The response.</returns>
    public async Task<IResult> GetHealthAsync(HttpContext http)
    {
        var context = new ModuleHttpServerCallContext(http, ModuleRestMethods.ReportHealth);

        try
        {
            await _authentication.UnaryServerHandler<object, object>(
                new object(), context, static (_, _) => Task.FromResult(new object()));
        }
        catch (RpcException ex)
        {
            return FromRpcException(http, ex);
        }

        return Json(new { status = "ok", moduleId = GetModuleId(context) });
    }

    private async Task<IResult> ExecuteAsync(
        HttpContext http,
        string rpcMethod,
        Func<ServerCallContext, Task<IResult>> handler)
    {
        var context = new ModuleHttpServerCallContext(http, rpcMethod);

        try
        {
            // Same order as the gRPC pipeline in SupervisorServiceExtensions. Each interceptor only
            // performs its check; the no-op continuation means "the call may proceed".
            await _authentication.UnaryServerHandler<object, object>(
                new object(), context, static (_, _) => Task.FromResult(new object()));

            await _callerContext.UnaryServerHandler<object, object>(
                new object(), context, static (_, _) => Task.FromResult(new object()));

            await _rateLimit.UnaryServerHandler<object, object>(
                new object(), context, static (_, _) => Task.FromResult(new object()));

            await _access.UnaryServerHandler<object, object>(
                new object(), context, static (_, _) => Task.FromResult(new object()));

            return await handler(context);
        }
        catch (RpcException ex)
        {
            return FromRpcException(http, ex);
        }
    }

    private static IResult FromRpcException(HttpContext http, RpcException exception)
    {
        var status = exception.StatusCode switch
        {
            StatusCode.Unauthenticated => StatusCodes.Status401Unauthorized,
            StatusCode.PermissionDenied => StatusCodes.Status403Forbidden,
            StatusCode.InvalidArgument => StatusCodes.Status400BadRequest,
            StatusCode.NotFound => StatusCodes.Status404NotFound,
            StatusCode.ResourceExhausted => StatusCodes.Status429TooManyRequests,
            StatusCode.Unimplemented => StatusCodes.Status501NotImplemented,
            StatusCode.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        var code = exception.Trailers.GetValue("error-code") ?? exception.StatusCode switch
        {
            StatusCode.Unauthenticated => ErrorCodes.Unauthorized,
            StatusCode.PermissionDenied => ErrorCodes.Forbidden,
            StatusCode.InvalidArgument => "INVALID_ARGUMENT",
            StatusCode.NotFound => "NOT_FOUND",
            StatusCode.ResourceExhausted => "MODULE_RATE_LIMITED",
            StatusCode.Unimplemented => "NOT_IMPLEMENTED",
            StatusCode.Unavailable => "SERVICE_UNAVAILABLE",
            _ => "INTERNAL_ERROR",
        };

        var error = new ApiErrorResponse(code, exception.Status.Detail)
        {
            Path = http.Request.Path,
            TraceId = Activity.Current?.Id,
        };

        // Consent denials carry the scopes the user still has to approve, so a REST caller can
        // launch the consent flow without guessing what is missing.
        var consentScopes = exception.Trailers.GetValue(ModuleConsentPolicy.MissingScopesTrailer);
        if (!string.IsNullOrWhiteSpace(consentScopes))
        {
            error.Details = new
            {
                consentScopes = consentScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            };
        }

        return Results.Json(error, statusCode: status);
    }

    private static IResult Json(object value) => Results.Json(value);

    private static object ToProfile(Api.ModuleUserProfile user) => new
    {
        id = user.Id,
        displayName = user.DisplayName,
        avatarUrl = user.AvatarUrl,
        isActive = user.IsActive,
        email = user.Email,
    };

    private static object ToGroup(Api.ModuleGroup group) => new
    {
        id = group.Id,
        organizationId = group.OrganizationId,
        name = group.Name,
        description = group.Description,
        isAllUsersGroup = group.IsAllUsersGroup,
        memberCount = group.MemberCount,
        createdAt = group.CreatedAt,
    };

    private static object ToTeam(Api.ModuleTeam team) => new
    {
        id = team.Id,
        organizationId = team.OrganizationId,
        name = team.Name,
        description = team.Description,
        memberCount = team.MemberCount,
        createdAt = team.CreatedAt,
    };

    private static object ToFileNode(Api.ModuleFileNode node) => new
    {
        id = node.Id,
        name = node.Name,
        nodeType = node.NodeType,
        mimeType = node.MimeType,
        size = node.Size,
        parentId = node.ParentId,
        updatedAt = node.UpdatedAt,
    };

    private static object ToFileNode(DotNetCloud.Core.Capabilities.FileNodeInfo node) => new
    {
        id = node.Id.ToString(),
        name = node.Name,
        nodeType = node.NodeType,
        mimeType = node.MimeType ?? string.Empty,
        size = node.Size,
        parentId = node.ParentId?.ToString() ?? string.Empty,
        updatedAt = node.UpdatedAt?.ToString("O") ?? string.Empty,
    };

    /// <summary>
    /// Gets the module and acting user a file call is made for.
    /// </summary>
    /// <param name="context">A call context that has completed the pipeline.</param>
    /// <returns>The verified module id and user id.</returns>
    private static (string ModuleId, Guid UserId) RequireActingUser(ServerCallContext context)
    {
        var binding = CallerSubjectGuard.Bind(context, requestedUserId: null);

        if (!binding.IsAllowed || binding.IsModuleScope || binding.UserId == Guid.Empty)
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "File access requires a verified end-user assertion."));
        }

        return (GetModuleId(context), binding.UserId);
    }
}
