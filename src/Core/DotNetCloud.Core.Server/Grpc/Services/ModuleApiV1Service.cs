using Api = DotNetCloud.Core.Grpc.ModuleApi.V1;
using Legacy = DotNetCloud.Core.Grpc.Capabilities;
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
/// <item><c>GetUser</c>/<c>SearchUsers</c> return a minimal profile — email is omitted until the
/// per-user consent plane (phase A3) can authorise it.</item>
/// <item><c>ReportHealth</c> is new and recorded in <see cref="ModuleHealthRegistry"/>.</item>
/// <item>The consent queries answer <see cref="StatusCode.Unimplemented"/> until A3 builds the
/// consent store, so a module cannot mistake "not implemented" for "no consent granted".</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class ModuleApiV1Service : Api.ModuleApi.ModuleApiBase
{
    private readonly CoreCapabilitiesServiceImpl _legacy;
    private readonly ModuleHealthRegistry _health;
    private readonly ILogger<ModuleApiV1Service> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleApiV1Service"/> class.</summary>
    /// <param name="legacy">The legacy capability handlers every RPC delegates to.</param>
    /// <param name="health">Self-reported module health.</param>
    /// <param name="logger">Logger.</param>
    public ModuleApiV1Service(
        CoreCapabilitiesServiceImpl legacy,
        ModuleHealthRegistry health,
        ILogger<ModuleApiV1Service> logger)
    {
        _legacy = legacy;
        _health = health;
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
                    // Email is withheld on the canonical surface until per-user consent (A3) exists.
                    Email = string.Empty,
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
            // Withheld until per-user consent (A3) — never turn this into a PII enumeration.
            Email = string.Empty,
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
    public override Task<Api.CheckUserConsentResponse> CheckUserConsent(
        Api.CheckUserConsentRequest request,
        ServerCallContext context) =>
        throw ConsentNotImplemented("CheckUserConsent");

    /// <inheritdoc />
    public override Task<Api.GetGrantedScopesResponse> GetGrantedScopes(
        Api.GetGrantedScopesRequest request,
        ServerCallContext context) =>
        throw ConsentNotImplemented("GetGrantedScopes");

    private static RpcException ConsentNotImplemented(string rpc) =>
        new(new Status(
            StatusCode.Unimplemented,
            $"{rpc} is not available yet: per-user consent (phase A3) is not implemented. " +
            "Treat the answer as unknown, not as 'no consent'."));

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
