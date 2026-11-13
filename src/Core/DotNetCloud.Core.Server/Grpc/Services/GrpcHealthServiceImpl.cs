using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
using Grpc.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Services;

/// <summary>
/// gRPC service implementation for core capabilities exposed to modules.
/// Modules connect to this service to access platform capabilities such as
/// user directory, notifications, event bus, and settings.
/// </summary>
internal class CoreCapabilitiesServiceImpl : CoreCapabilities.CoreCapabilitiesBase
{
    private readonly ILogger<CoreCapabilitiesServiceImpl> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SearchIndexingService _indexingService;
    private readonly ModuleEventInbox _eventInbox;
    private readonly ModuleConsentPolicy _consentPolicy;

    public CoreCapabilitiesServiceImpl(
        ILogger<CoreCapabilitiesServiceImpl> logger,
        IServiceProvider serviceProvider,
        SearchIndexingService indexingService,
        ModuleEventInbox eventInbox,
        ModuleConsentPolicy consentPolicy)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _indexingService = indexingService;
        _eventInbox = eventInbox;
        _consentPolicy = consentPolicy;
    }

    /// <summary>
    /// Looks up user information by ID (IUserDirectory capability).
    /// </summary>
    public override async Task<GetUserResponse> GetUser(GetUserRequest request, ServerCallContext context)
    {
        _logger.LogDebug("GetUser called for {UserId} by module {ModuleId}",
            request.UserId, GetModuleId(context));

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            _logger.LogWarning("GetUser called with invalid UserId '{RawId}'", LogSanitizer.Sanitize(request.UserId));
            return new GetUserResponse { Found = false };
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var userDir = scope.ServiceProvider.GetRequiredService<IUserDirectory>();

            var names = await userDir.GetDisplayNamesAsync(new[] { userId });
            if (!names.TryGetValue(userId, out var displayName))
            {
                _logger.LogInformation("GetUser: user {UserId} not found", userId);
                return new GetUserResponse { Found = false };
            }

            var avatars = await userDir.GetAvatarUrlsAsync(new[] { userId });
            avatars.TryGetValue(userId, out var avatarUrl);

            _logger.LogDebug("GetUser: found user {UserId} ('{DisplayName}')", userId, displayName);

            return new GetUserResponse
            {
                Found = true,
                User = new UserInfo
                {
                    Id = request.UserId,
                    DisplayName = displayName,
                    AvatarUrl = avatarUrl ?? string.Empty,
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetUser failed for {UserId}", request.UserId);
            return new GetUserResponse { Found = false };
        }
    }

    /// <summary>
    /// Searches users matching criteria (IUserDirectory capability).
    /// </summary>
    /// <remarks>
    /// Email addresses are personal data: a result carries one only when the target user has
    /// consented to the <c>profile</c> scope for the calling module. Names and avatars stay
    /// available because a module cannot render a member list without them, and the search itself
    /// is already gated by the capability grant.
    /// </remarks>
    public override async Task<SearchUsersResponse> SearchUsers(SearchUsersRequest request, ServerCallContext context)
    {
        _logger.LogDebug("SearchUsers called with query '{Query}' by module {ModuleId}",
            LogSanitizer.Sanitize(request.Query), LogSanitizer.Sanitize(GetModuleId(context)));

        try
        {
            var moduleId = GetModuleId(context);

            using var scope = _serviceProvider.CreateScope();
            var userDir = scope.ServiceProvider.GetRequiredService<IUserDirectory>();

            var results = await userDir.SearchUsersAsync(request.Query, request.MaxResults > 0 ? request.MaxResults : 20);

            var response = new SearchUsersResponse();
            foreach (var result in results)
            {
                var maySeeEmail = await _consentPolicy.HasConsentAsync(
                    moduleId,
                    result.Id,
                    ModuleScopes.Profile,
                    context.CancellationToken);

                response.Users.Add(new UserInfo
                {
                    Id = result.Id.ToString(),
                    DisplayName = result.DisplayName,
                    Email = maySeeEmail ? result.Email : string.Empty,
                });
            }

            _logger.LogDebug("SearchUsers: found {Count} results for query '{Query}'",
                response.Users.Count, LogSanitizer.Sanitize(request.Query));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchUsers failed for query '{Query}'", LogSanitizer.Sanitize(request.Query));
            return new SearchUsersResponse();
        }
    }

    /// <summary>
    /// Gets the current caller's identity (ICurrentUserContext capability).
    /// </summary>
    /// <remarks>
    /// The acting identity is read only from the verified <c>CallerContext</c> injected by
    /// <c>CallerContextInterceptor</c> (core-signed user assertion re-validated per call).
    /// <c>request.Caller</c> is caller-supplied and is deliberately ignored: trusting it let any
    /// module read another user's identity.
    /// </remarks>
    public override async Task<GetCurrentUserResponse> GetCurrentUser(
        GetCurrentUserRequest request, ServerCallContext context)
    {
        var moduleId = GetModuleId(context);
        _logger.LogDebug("GetCurrentUser called by module {ModuleId}", moduleId);

        if (!context.UserState.TryGetValue("CallerContext", out var callerState)
            || callerState is not CallerContext caller)
        {
            // CallerContextInterceptor runs before this service; reaching this point means the
            // interceptor chain was misconfigured. Fail closed rather than inventing an identity.
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "No verified caller identity is available for this call"));
        }

        if (caller.Type is not CallerType.User)
        {
            // A module acting for itself has no end-user identity to report.
            return new GetCurrentUserResponse
            {
                UserId = string.Empty,
                DisplayName = string.Empty,
                Email = string.Empty,
                Locale = "en-US",
                Timezone = "UTC",
            };
        }

        var response = new GetCurrentUserResponse
        {
            UserId = caller.UserId.ToString(),
            Email = string.Empty,
            Locale = "en-US",
            Timezone = "UTC",
        };

        response.Roles.AddRange(caller.Roles);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var userDir = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
            var names = await userDir.GetDisplayNamesAsync([caller.UserId], context.CancellationToken);

            if (names.TryGetValue(caller.UserId, out var displayName))
            {
                response.DisplayName = displayName;
            }
        }
        catch (Exception ex)
        {
            // The identity fields above are authoritative; a directory hiccup only costs the
            // display name.
            _logger.LogWarning(ex, "GetCurrentUser could not resolve the display name for module {ModuleId}", moduleId);
        }

        return response;
    }

    /// <summary>
    /// Sends a notification (INotificationService capability).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every notification is attributed to the authenticated module (<c>SourceModuleId</c>), never
    /// to a caller-supplied field, so a module cannot post platform-branded messages as another
    /// module.
    /// </para>
    /// <para>
    /// Content is untrusted: title and body are length-capped, and the action link must be a
    /// site-relative path. Core navigates the browser to that link, so an absolute URL would let a
    /// module turn a notification into a phishing hop out of the platform (F6). Out-of-policy
    /// content fails the call with <c>MODULE_INVALID_REQUEST</c> rather than being silently
    /// mangled.
    /// </para>
    /// </remarks>
    public override async Task<SendNotificationResponse> SendNotification(SendNotificationRequest request, ServerCallContext context)
    {
        var moduleId = GetModuleId(context);

        if (!ModuleNotificationLimits.IsValidTitle(request.Title)
            || !ModuleNotificationLimits.IsValidBody(request.Body)
            || !ModuleNotificationLimits.IsValidLink(request.Link))
        {
            _logger.LogWarning(
                "SendNotification rejected from module {ModuleId}: content is out of policy " +
                "(title {TitleLength}, body {BodyLength}, link {LinkLength} characters)",
                LogSanitizer.Sanitize(moduleId),
                request.Title?.Length ?? 0,
                request.Body?.Length ?? 0,
                request.Link?.Length ?? 0);

            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    "[MODULE_INVALID_REQUEST] Notification title/body is too long or the action " +
                    "link is not a site-relative path."),
                new Metadata { { "error-code", ErrorCodes.ModuleInvalidRequest } });
        }

        _logger.LogInformation(
            "SendNotification: '{Title}' to {Count} recipients from module {ModuleId}",
            LogSanitizer.Sanitize(request.Title), request.RecipientUserIds.Count, moduleId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            // The category the caller supplied decides how the notification is stored. It used to be
            // accepted and then dropped, so every module-sent notification — calendar reminders
            // included — was persisted as a generic Info/Normal entry.
            var (type, priority) = MapNotificationCategory(request.Category);

            // Delivery honours per-user consent when the module declared `notifications:send`:
            // "this module may notify me" is the user's decision, not the admin's.
            var recipients = await FilterNotificationRecipientsAsync(
                moduleId, request.RecipientUserIds, context.CancellationToken);

            if (recipients.Count != request.RecipientUserIds.Distinct().Count())
            {
                _logger.LogDebug(
                    "SendNotification from module {ModuleId} filtered recipients by consent: {Delivered} of {Requested}",
                    LogSanitizer.Sanitize(moduleId),
                    recipients.Count,
                    request.RecipientUserIds.Count);
            }

            var deliveredCount = 0;

            foreach (var userId in recipients)
            {
                var notification = new NotificationDto
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    SourceModuleId = moduleId,
                    Type = type,
                    Title = request.Title,
                    Message = string.IsNullOrEmpty(request.Body) ? null : request.Body,
                    Priority = priority,
                    ActionUrl = string.IsNullOrEmpty(request.Link) ? null : request.Link,
                    CreatedAtUtc = DateTime.UtcNow,
                };

                await notificationService.SendAsync(userId, notification, context.CancellationToken);
                deliveredCount++;
            }

            return new SendNotificationResponse
            {
                Success = true,
                DeliveredCount = deliveredCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendNotification failed for module {ModuleId}", moduleId);
            return new SendNotificationResponse { Success = false, DeliveredCount = 0 };
        }
    }

    /// <summary>
    /// Rejects a user-scoped directory call when the module declared the <c>profile</c> scope and
    /// the user has not consented to it.
    /// </summary>
    /// <param name="context">The gRPC call context.</param>
    /// <param name="userId">The user the call targets (already bound to the caller).</param>
    /// <param name="rpc">The RPC name, for the audit trail.</param>
    private async Task EnforceProfileConsentAsync(ServerCallContext context, Guid userId, string rpc)
    {
        var moduleId = GetModuleId(context);

        var missing = await _consentPolicy.GetMissingConsentScopesAsync(
            moduleId,
            userId,
            [ModuleScopes.Profile],
            context.CancellationToken);

        if (missing.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "{Rpc} denied for module {ModuleId}: user {UserId} has not consented to {Scopes}",
            rpc,
            LogSanitizer.Sanitize(moduleId),
            userId,
            string.Join(' ', missing));

        throw ModuleConsentPolicy.CreateConsentRequiredException(missing);
    }

    /// <summary>
    /// Gets the recipients of a notification, dropping any user who has not consented to
    /// <c>notifications:send</c> from a module that declared it.
    /// </summary>
    /// <param name="moduleId">The authenticated module id.</param>
    /// <param name="recipientIds">The requested recipients.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recipients the module may notify.</returns>
    private async Task<IReadOnlyList<Guid>> FilterNotificationRecipientsAsync(
        string moduleId,
        IEnumerable<string> recipientIds,
        CancellationToken cancellationToken)
    {
        var parsed = recipientIds
            .Select(id => Guid.TryParse(id, out var userId) ? userId : Guid.Empty)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();

        var declared = _consentPolicy.GetDeclaredScopes(moduleId);
        if (declared is null || !declared.Contains(ModuleScopes.NotificationsSend, StringComparer.Ordinal))
        {
            // The module did not opt into the consent plane for notifications; the capability grant
            // is the only gate, exactly as before.
            return parsed;
        }

        var allowed = new List<Guid>(parsed.Length);
        foreach (var userId in parsed)
        {
            if (await _consentPolicy.HasConsentAsync(moduleId, userId, ModuleScopes.NotificationsSend, cancellationToken))
            {
                allowed.Add(userId);
            }
        }

        return allowed;
    }

    /// <remarks>
    /// A reminder is a time-sensitive, high-priority notification and must not be filed as generic
    /// information, which is how every module-sent notification was stored while this field was ignored.
    /// Unknown or absent categories keep the generic defaults.
    /// </remarks>
    /// <param name="category">The category supplied by the caller (may be null or empty).</param>
    /// <returns>The notification type and priority to store.</returns>
    internal static (NotificationType Type, NotificationPriority Priority) MapNotificationCategory(
        string? category)
        => string.Equals(category, "Reminder", StringComparison.OrdinalIgnoreCase)
            ? (NotificationType.Reminder, NotificationPriority.High)
            : (NotificationType.Info, NotificationPriority.Normal);

    /// <summary>
    /// Publishes a platform event on the core's event bus (IEventBus capability).
    /// </summary>
    /// <remarks>
    /// This is the supported way for a process-isolated module to raise a platform event: the module
    /// reports what happened and the core owns delivery (in-app notification, real-time push, email).
    /// The publisher's identity is the authenticated module on the transport — there is no caller
    /// field in the request, so a module can neither impersonate another module nor claim system
    /// identity, and only event types registered in <see cref="ModuleEventInbox"/> are accepted.
    /// </remarks>
    public override async Task<PublishEventResponse> PublishEvent(
        PublishEventRequest request,
        ServerCallContext context)
    {
        var moduleId = GetModuleId(context);

        if (!context.UserState.TryGetValue("CallerContext", out var callerValue)
            || callerValue is not CallerContext caller)
        {
            // CallerContextInterceptor always populates this for an authenticated module. Reaching
            // here means the interceptor chain is misconfigured: refuse rather than guess an identity.
            _logger.LogError(
                "PublishEvent: no caller context on an authenticated call from module {ModuleId}", moduleId);
            return new PublishEventResponse
            {
                Success = false,
                Error = "The caller identity could not be established."
            };
        }

        var (success, error) = await _eventInbox.TryPublishAsync(
            request.EventType,
            request.PayloadJson,
            caller,
            moduleId,
            context.CancellationToken);

        if (success)
        {
            _logger.LogInformation(
                "PublishEvent: {EventType} from module {ModuleId} published on the core event bus",
                request.EventType, moduleId);
        }

        return new PublishEventResponse { Success = success, Error = error ?? string.Empty };
    }

    /// <summary>
    /// Enqueues a real-time search-index request from a process-isolated module.
    /// </summary>
    /// <remarks>
    /// The index entry is attributed to the <em>authenticated</em> module identity, never to the
    /// <c>module_id</c> field in the request. A module could otherwise index, overwrite or remove
    /// entries on behalf of another module.
    /// </remarks>
    public override async Task<SubmitSearchIndexResponse> SubmitSearchIndex(
        SubmitSearchIndexRequest request, ServerCallContext context)
    {
        var moduleId = GetModuleId(context);

        if (request.Action is < 0 or > 1)
        {
            _logger.LogWarning(
                "SubmitSearchIndex: invalid action {Action} for {EntityId} from module {ModuleId}",
                request.Action, LogSanitizer.Sanitize(request.EntityId), moduleId);
            return new SubmitSearchIndexResponse { Success = false };
        }

        await _indexingService.EnqueueAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = moduleId,
            EntityId = request.EntityId,
            Action = (SearchIndexAction)request.Action
        });

        return new SubmitSearchIndexResponse { Success = true };
    }

    /// <summary>
    /// Gets a module setting value (IModuleSettings capability).
    /// </summary>
    /// <remarks>
    /// The key is namespaced by the <em>authenticated</em> module identity; <c>request.ModuleId</c>
    /// is caller-supplied and ignored, so a module cannot read another module's settings.
    /// </remarks>
    public override async Task<GetSettingResponse> GetSetting(GetSettingRequest request, ServerCallContext context)
    {
        var moduleId = GetModuleId(context);

        if (!ModuleSettingLimits.IsValidKey(request.Key))
        {
            _logger.LogWarning(
                "GetSetting: rejected invalid key from module {ModuleId}: {Key}",
                moduleId, LogSanitizer.Sanitize(request.Key));
            return new GetSettingResponse { Found = false };
        }

        using var scope = _serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IModuleSettingsStore>();
        var value = await store.GetAsync(moduleId, request.Key, context.CancellationToken);

        return value is null
            ? new GetSettingResponse { Found = false }
            : new GetSettingResponse { Found = true, Value = value };
    }

    /// <summary>
    /// Sets a module setting value (IModuleSettings capability).
    /// </summary>
    /// <remarks>
    /// The key is namespaced by the <em>authenticated</em> module identity; <c>request.ModuleId</c>
    /// is caller-supplied and ignored. Keys and values are length- and charset-capped before they
    /// reach the database.
    /// </remarks>
    public override async Task<SetSettingResponse> SetSetting(SetSettingRequest request, ServerCallContext context)
    {
        var moduleId = GetModuleId(context);

        if (!ModuleSettingLimits.IsValidKey(request.Key) || !ModuleSettingLimits.IsValidValue(request.Value))
        {
            _logger.LogWarning(
                "SetSetting: rejected invalid key or oversized value from module {ModuleId}: {Key}",
                moduleId, LogSanitizer.Sanitize(request.Key));
            return new SetSettingResponse { Success = false };
        }

        var actingUserId = context.UserState.TryGetValue("CallerContext", out var callerState)
            && callerState is CallerContext { Type: CallerType.User } caller
                ? caller.UserId
                : (Guid?)null;

        using var scope = _serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IModuleSettingsStore>();
        await store.SetAsync(
            moduleId,
            request.Key,
            request.Value,
            isSecret: false,
            updatedByUserId: actingUserId,
            context.CancellationToken);

        return new SetSettingResponse { Success = true };
    }

    /// <summary>
    /// Gets basic group information by ID (IGroupDirectory capability).
    /// </summary>
    public override async Task<GetGroupResponse> GetGroup(GetGroupRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.GroupId, out var groupId))
        {
            _logger.LogWarning("GetGroup called with invalid GroupId '{RawId}'", LogSanitizer.Sanitize(request.GroupId));
            return new GetGroupResponse { Found = false };
        }

        _logger.LogInformation("GetGroup called for {GroupId} by module {ModuleId}",
            groupId, GetModuleId(context));

        try
        {
            // IGroupDirectory is scoped (it depends on the scoped CoreDbContext), but this
            // gRPC service is a singleton holding the root provider. Create a scope so the
            // scoped service can be resolved — resolving it from the root provider throws.
            using var scope = _serviceProvider.CreateScope();
            var groupDirectory = scope.ServiceProvider.GetRequiredService<IGroupDirectory>();
            var group = await groupDirectory.GetGroupAsync(groupId, context.CancellationToken);

            if (group is null)
            {
                _logger.LogInformation("GetGroup: group {GroupId} not found in database", groupId);
                return new GetGroupResponse { Found = false };
            }

            _logger.LogInformation("GetGroup: found group {GroupId} ('{GroupName}') for org {OrgId}",
                groupId, group.Name, group.OrganizationId);

            return new GetGroupResponse
            {
                Found = true,
                Group = new GroupInfoMessage
                {
                    Id = group.Id.ToString(),
                    OrganizationId = group.OrganizationId.ToString(),
                    Name = group.Name,
                    Description = group.Description ?? string.Empty,
                    IsAllUsersGroup = group.IsAllUsersGroup,
                    MemberCount = group.MemberCount,
                    CreatedAt = group.CreatedAt.ToString("O"),
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGroup failed for {GroupId}", groupId);
            return new GetGroupResponse { Found = false };
        }
    }

    /// <summary>
    /// Gets basic team information by ID (ITeamDirectory capability).
    /// </summary>
    public override async Task<GetTeamResponse> GetTeam(GetTeamRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.TeamId, out var teamId))
        {
            _logger.LogWarning("GetTeam called with invalid TeamId '{RawId}'", LogSanitizer.Sanitize(request.TeamId));
            return new GetTeamResponse { Found = false };
        }

        _logger.LogInformation("GetTeam called for {TeamId} by module {ModuleId}",
            teamId, GetModuleId(context));

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var teamDirectory = scope.ServiceProvider.GetRequiredService<ITeamDirectory>();
            var team = await teamDirectory.GetTeamAsync(teamId, context.CancellationToken);

            if (team is null)
            {
                _logger.LogInformation("GetTeam: team {TeamId} not found in database", teamId);
                return new GetTeamResponse { Found = false };
            }

            _logger.LogInformation("GetTeam: found team {TeamId} ('{TeamName}') for org {OrgId}",
                teamId, team.Name, team.OrganizationId);

            return new GetTeamResponse
            {
                Found = true,
                Team = new TeamInfoMessage
                {
                    Id = team.Id.ToString(),
                    OrganizationId = team.OrganizationId.ToString(),
                    Name = team.Name,
                    Description = team.Description ?? string.Empty,
                    MemberCount = team.MemberCount,
                    CreatedAt = team.CreatedAt.ToString("O"),
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTeam failed for {TeamId}", teamId);
            return new GetTeamResponse { Found = false };
        }
    }

    /// <summary>
    /// Lists the teams a given user belongs to (ITeamDirectory capability).
    /// </summary>
    /// <remarks>
    /// The target user is bound to the verified caller: a call carrying a user assertion may only
    /// ask for that user's teams, and naming someone else is rejected (F4). Module-scope calls
    /// (background work, no assertion) stay subject to the capability grant and are audited.
    /// </remarks>
    public override async Task<GetTeamsForUserResponse> GetTeamsForUser(GetTeamsForUserRequest request, ServerCallContext context)
    {
        var binding = await BindSubjectAsync(context, request.UserId, "GetTeamsForUser");
        if (binding.UserId == Guid.Empty)
        {
            return new GetTeamsForUserResponse();
        }

        var userId = binding.UserId;

        _logger.LogInformation("GetTeamsForUser called for {UserId} by module {ModuleId}",
            userId, GetModuleId(context));

        await EnforceProfileConsentAsync(context, userId, "GetTeamsForUser");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var teamDirectory = scope.ServiceProvider.GetRequiredService<ITeamDirectory>();
            var teams = await teamDirectory.GetTeamsForUserAsync(userId, context.CancellationToken);

            var response = new GetTeamsForUserResponse();
            response.Teams.AddRange(teams.Select(t => new TeamInfoMessage
            {
                Id = t.Id.ToString(),
                OrganizationId = t.OrganizationId.ToString(),
                Name = t.Name,
                Description = t.Description ?? string.Empty,
                MemberCount = t.MemberCount,
                CreatedAt = t.CreatedAt.ToString("O"),
            }));

            _logger.LogDebug("GetTeamsForUser: found {Count} teams for user {UserId}", response.Teams.Count, userId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTeamsForUser failed for {UserId}", userId);
            return new GetTeamsForUserResponse();
        }
    }

    /// <summary>
    /// Lists the organizations a user is an active member of (IOrganizationDirectory capability).
    /// </summary>
    /// <remarks>
    /// A module host has no access to <c>core.organizations</c>, so without this RPC a sandboxed
    /// module UI cannot offer an organization picker and has to ask the user to paste a GUID. The
    /// subject is bound to the verified caller exactly as for <see cref="GetTeamsForUser"/>: naming
    /// another user is rejected, and the call is subject to the <c>profile</c> consent scope when the
    /// module declared it.
    /// </remarks>
    public override async Task<GetUserOrganizationsResponse> GetUserOrganizations(
        GetUserOrganizationsRequest request, ServerCallContext context)
    {
        var binding = await BindSubjectAsync(context, request.UserId, "GetUserOrganizations");
        if (binding.UserId == Guid.Empty)
        {
            return new GetUserOrganizationsResponse();
        }

        var userId = binding.UserId;

        _logger.LogDebug("GetUserOrganizations called for {UserId} by module {ModuleId}",
            userId, GetModuleId(context));

        await EnforceProfileConsentAsync(context, userId, "GetUserOrganizations");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var organizationDirectory = scope.ServiceProvider.GetRequiredService<IOrganizationDirectory>();
            var organizations = await organizationDirectory.GetUserOrganizationsAsync(userId, context.CancellationToken);

            var response = new GetUserOrganizationsResponse();
            response.Organizations.AddRange(organizations.Select(o => new OrganizationInfoMessage
            {
                Id = o.Id.ToString(),
                Name = o.Name,
                Description = o.Description ?? string.Empty,
                MemberCount = o.MemberCount,
                TeamCount = o.TeamCount,
                CreatedAt = o.CreatedAt.ToString("O"),
            }));

            _logger.LogDebug("GetUserOrganizations: found {Count} organizations for user {UserId}",
                response.Organizations.Count, userId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetUserOrganizations failed for {UserId}", userId);
            return new GetUserOrganizationsResponse();
        }
    }

    /// <summary>
    /// Lists the groups a user belongs to (IGroupDirectory capability). Module hosts use this to
    /// resolve admin shared-folder group grants; without it a host cannot tell whether a caller
    /// is entitled to a folder, and must deny by default.
    /// </summary>
    /// <remarks>
    /// As with <see cref="GetTeamsForUser"/>, a user-scoped call may only ask for its own
    /// membership; naming another user is rejected (F4).
    /// </remarks>
    public override async Task<GetGroupsForUserResponse> GetGroupsForUser(
        GetGroupsForUserRequest request, ServerCallContext context)
    {
        var binding = await BindSubjectAsync(context, request.UserId, "GetGroupsForUser");
        if (binding.UserId == Guid.Empty)
        {
            return new GetGroupsForUserResponse();
        }

        var userId = binding.UserId;

        _logger.LogDebug("GetGroupsForUser called for {UserId} by module {ModuleId}",
            userId, GetModuleId(context));

        await EnforceProfileConsentAsync(context, userId, "GetGroupsForUser");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var groupDirectory = scope.ServiceProvider.GetRequiredService<IGroupDirectory>();
            var groups = await groupDirectory.GetGroupsForUserAsync(userId, context.CancellationToken);

            var response = new GetGroupsForUserResponse();
            response.Groups.AddRange(groups.Select(g => new GroupInfoMessage
            {
                Id = g.Id.ToString(),
                OrganizationId = g.OrganizationId.ToString(),
                Name = g.Name,
                Description = g.Description ?? string.Empty,
                IsAllUsersGroup = g.IsAllUsersGroup,
                MemberCount = g.MemberCount,
                CreatedAt = g.CreatedAt.ToString("O"),
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGroupsForUser failed for {UserId}", userId);
            return new GetGroupsForUserResponse();
        }
    }

    /// <summary>
    /// Triggers cleanup of orphaned media sources and entities after an
    /// admin shared folder is deleted. Called by the Files module host.
    /// </summary>
    public override async Task<CleanupAdminSharedFolderResponse> CleanupAdminSharedFolder(
        CleanupAdminSharedFolderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SharedFolderId, out var sharedFolderId))
        {
            return new CleanupAdminSharedFolderResponse
            {
                Success = false,
                ErrorMessage = "Invalid shared_folder_id"
            };
        }

        _logger.LogInformation(
            "CleanupAdminSharedFolder called for {SharedFolderId} ('{DisplayName}') by module {ModuleId}",
            sharedFolderId, request.DisplayName, GetModuleId(context));

        try
        {
            // Resolve the cleanup service from DI
            var cleanupService = _serviceProvider.GetRequiredService<AdminSharedFolderCleanupService>();

            // Build the event from the gRPC request
            var evt = new AdminSharedFolderDeletedEvent
            {
                EventId = Guid.CreateVersion7(),
                CreatedAt = DateTime.UtcNow,
                SharedFolderId = sharedFolderId,
                DisplayName = request.DisplayName,
                MountedEntries = request.MountedEntries
                    .Select(e => new MountedEntryInfo
                    {
                        RelativePath = e.RelativePath,
                        IsDirectory = e.IsDirectory,
                    })
                    .ToList(),
            };

            await cleanupService.HandleAsync(evt, context.CancellationToken);

            return new CleanupAdminSharedFolderResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupAdminSharedFolder failed for {SharedFolderId}", sharedFolderId);
            return new CleanupAdminSharedFolderResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Broadcasts a real-time event to connected SignalR clients.
    /// Called by process-isolated module hosts to push events (new messages,
    /// typing indicators, etc.) through Core.Server's SignalR infrastructure.
    /// </summary>
    /// <remarks>
    /// When a module's manifest declares a real-time event vocabulary, the name must be in it — a
    /// module that can broadcast any name can drive UI surfaces it does not own (a fake
    /// <c>NewMessage</c> in a chat channel, for instance). Modules that declare nothing keep the
    /// previous behaviour while the vocabulary is rolled out across the in-repo manifests.
    /// </remarks>
    public override async Task<BroadcastRealtimeEventResponse> BroadcastRealtimeEvent(
        BroadcastRealtimeEventRequest request, ServerCallContext context)
    {
        var moduleId = GetModuleId(context);
        _logger.LogDebug(
            "BroadcastRealtimeEvent: group={Group}, event={Event} from module {ModuleId}",
            request.Group, request.EventName, moduleId);

        await EnforceDeclaredRealtimeAsync(context, moduleId, request.EventName);

        try
        {
            var broadcaster = _serviceProvider.GetRequiredService<IRealtimeBroadcaster>();

            if (!string.IsNullOrEmpty(request.TargetUserId)
                && Guid.TryParse(request.TargetUserId, out var targetUserId))
            {
                // Per-user delivery
                object? payload = null;
                if (!string.IsNullOrEmpty(request.PayloadJson))
                    payload = JsonSerializer.Deserialize<object>(request.PayloadJson);

                await broadcaster.SendToUserAsync(targetUserId, request.EventName, payload ?? request.PayloadJson, context.CancellationToken);

                if (request.EventName is "CalendarEventCreated" or "CalendarEventUpdated" or "CalendarEventDeleted")
                {
                    try
                    {
                        using var pushScope = _serviceProvider.CreateScope();
                        var chatApiClient = pushScope.ServiceProvider
                            .GetRequiredService<DotNetCloud.Core.Services.ModuleApis.IChatApiClient>();

                        var eventId = string.Empty;
                        if (!string.IsNullOrEmpty(request.PayloadJson))
                        {
                            using var jsonDoc = JsonDocument.Parse(request.PayloadJson);
                            if (jsonDoc.RootElement.TryGetProperty("eventId", out var evtIdProp))
                                eventId = evtIdProp.GetString() ?? string.Empty;
                        }

                        await chatApiClient.SendPushNotificationAsync(
                            targetUserId,
                            request.EventName switch
                            {
                                "CalendarEventCreated" => "New Event",
                                "CalendarEventUpdated" => "Calendar Updated",
                                "CalendarEventDeleted" => "Event Cancelled",
                                _ => "Calendar Update"
                            },
                            string.Empty,
                            "System",
                            new Dictionary<string, string>
                            {
                                ["type"] = "calendar_event",
                                ["eventId"] = eventId
                            },
                            context.CancellationToken);
                    }
                    catch (Exception pushEx)
                    {
                        _logger.LogWarning(pushEx, "Failed to send push for calendar event {EventName} to user {UserId}",
                            request.EventName, targetUserId);
                    }
                }
            }
            else
            {
                // Group broadcast
                object? payload = null;
                if (!string.IsNullOrEmpty(request.PayloadJson))
                    payload = JsonSerializer.Deserialize<object>(request.PayloadJson);

                // A typing heartbeat must never be echoed back to the connections of the user
                // who is typing — the typist must not see their own "X is typing…" indicator on
                // any of their own devices/tabs. Exclude that user's connections from the group
                // send; every other channel member still receives the heartbeat and clears it
                // when the typist's message arrives or the heartbeat expires.
                if (string.Equals(request.EventName, "TypingIndicator", StringComparison.Ordinal)
                    && TryGetTypingUserId(request.PayloadJson, out var typingUserId))
                {
                    var chatBroadcaster = _serviceProvider.GetRequiredService<RealtimeBroadcasterService>();
                    await chatBroadcaster.BroadcastToGroupExceptUserAsync(
                        request.Group, typingUserId, request.EventName,
                        payload ?? request.PayloadJson, context.CancellationToken);
                }
                else
                {
                    await broadcaster.BroadcastAsync(request.Group, request.EventName, payload ?? request.PayloadJson, context.CancellationToken);
                }
            }

            // Forward chat events to the in-process IChatMessageNotifier so Blazor Server
            // components (which rely on in-process events, not SignalR HubConnections) receive
            // real-time updates from process-isolated module hosts.
            TryForwardToChatMessageNotifier(request, moduleId);

            return new BroadcastRealtimeEventResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BroadcastRealtimeEvent failed for group={Group}, event={Event} from module {ModuleId}",
                request.Group, request.EventName, moduleId);
            return new BroadcastRealtimeEventResponse { Success = false };
        }
    }

    /// <summary>
    /// Records an audit trail entry (IAuditLogger capability, SOC 2 CC4).
    /// Called by process-isolated module hosts to persist create/update/delete/share
    /// operations to the <c>AuditLog</c> table in Core.Server.
    /// </summary>
    public override async Task<LogAuditResponse> LogAudit(LogAuditRequest request, ServerCallContext context)
    {
        var moduleId = GetModuleId(context);
        _logger.LogDebug("LogAudit called for {EntityType} by module {ModuleId}",
            request.EntityType, moduleId);

        try
        {
            // The caller context injected by CallerContextInterceptor from the verified module
            // identity is authoritative. The request message is only a fallback, and it can never
            // yield a system identity: the module transport cannot speak for System.
            var caller = context.UserState.TryGetValue("CallerContext", out var cc) && cc is CallerContext ctx
                ? ctx
                : MapCallerContext(request.Caller, moduleId);

            if (!Guid.TryParse(request.EntityId, out var entityId))
            {
                _logger.LogWarning("LogAudit called with invalid EntityId '{RawId}'", LogSanitizer.Sanitize(request.EntityId));
                return new LogAuditResponse { Success = false };
            }

            var entry = new AuditEntry
            {
                Caller = caller,
                // Attribution comes from the verified credential, never from the payload: a module
                // must not be able to write audit entries as another module.
                ModuleId = moduleId,
                Action = Enum.IsDefined(typeof(AuditAction), request.Action)
                    ? (AuditAction)request.Action
                    : AuditAction.Read,
                EntityType = request.EntityType,
                EntityId = entityId,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description,
            };

            using var scope = _serviceProvider.CreateScope();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            await auditLogger.LogAsync(entry, context.CancellationToken);

            return new LogAuditResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LogAudit failed for {EntityType} by module {ModuleId}", request.EntityType, moduleId);
            return new LogAuditResponse { Success = false };
        }
    }

    /// <summary>
    /// Maps a <see cref="CallerContextMessage"/> to a <see cref="CallerContext"/> for a caller
    /// that has already been authenticated as <paramref name="moduleId"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="CallerType.System"/> is intentionally unreachable here: system identities are
    /// created in-process by the core, never asserted over the module transport.
    /// </remarks>
    private static CallerContext MapCallerContext(CallerContextMessage? message, string moduleId)
    {
        var userId = message is not null && Guid.TryParse(message.UserId, out var uid) ? uid : Guid.Empty;

        return userId == Guid.Empty
            ? new CallerContext(ModuleCallerIdentity.ToUserId(moduleId), [], CallerType.Module)
            : new CallerContext(userId, message!.Roles.ToArray(), CallerType.User);
    }

    /// <summary>
    /// Forwards chat-related real-time events from a process-isolated module host
    /// to the in-process <see cref="IChatMessageNotifier"/>, which Blazor Server
    /// components subscribe to for real-time UI updates.
    /// </summary>
    private void TryForwardToChatMessageNotifier(BroadcastRealtimeEventRequest request, string moduleId)
    {
        // Only handle chat-channel events (e.g., "chat-channel-{guid}")
        if (request.Group is null || !request.Group.StartsWith("chat-channel-", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(request.PayloadJson))
            return;

        try
        {
            var notifier = _serviceProvider.GetService<IChatMessageNotifier>();
            if (notifier is null)
                return;

            using var doc = JsonDocument.Parse(request.PayloadJson);
            var root = doc.RootElement;

            switch (request.EventName)
            {
                case "NewMessage":
                case "MessageEdited":
                {
                    if (!root.TryGetProperty("channelId", out var chEl) || !chEl.TryGetGuid(out var channelId))
                        return;
                    if (!root.TryGetProperty("message", out var msgEl))
                        return;

                    var message = JsonSerializer.Deserialize<MessageDto>(msgEl.GetRawText(), JsonSerializerOptions.Default);
                    if (message is null)
                        return;

                    if (request.EventName == "NewMessage")
                        notifier.NotifyMessageReceived(channelId, message);
                    else
                        notifier.NotifyMessageEdited(channelId, message);

                    _logger.LogDebug("Forwarded {Event} to IChatMessageNotifier for channel {ChannelId}",
                        request.EventName, channelId);
                    break;
                }
                case "MessageDeleted":
                {
                    if (!root.TryGetProperty("channelId", out var chEl) || !chEl.TryGetGuid(out var channelId))
                        return;
                    if (!root.TryGetProperty("messageId", out var msgIdEl) || !msgIdEl.TryGetGuid(out var messageId))
                        return;

                    notifier.NotifyMessageDeleted(channelId, messageId);

                    _logger.LogDebug("Forwarded MessageDeleted to IChatMessageNotifier for channel {ChannelId}",
                        channelId);
                    break;
                }
                case "TypingIndicator":
                {
                    if (!root.TryGetProperty("channelId", out var chEl) || !chEl.TryGetGuid(out var channelId))
                        return;
                    if (!root.TryGetProperty("userId", out var uEl) || !uEl.TryGetGuid(out var userId))
                        return;

                    string? displayName = null;
                    if (root.TryGetProperty("displayName", out var dnEl) && dnEl.ValueKind == JsonValueKind.String)
                        displayName = dnEl.GetString();

                    notifier.NotifyTypingChanged(new ChatTypingNotification(channelId, userId, displayName));

                    _logger.LogDebug("Forwarded TypingIndicator to IChatMessageNotifier for channel {ChannelId}",
                        channelId);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to forward {Event} to IChatMessageNotifier from module {ModuleId}",
                request.EventName, moduleId);
        }
    }

    /// <summary>
    /// Rejects a real-time event the calling module did not declare in its manifest.
    /// </summary>
    /// <remarks>
    /// An empty declaration means the vocabulary has not been added to that module's manifest yet;
    /// those modules keep working unchanged. Once declared, the list is enforced exactly.
    /// </remarks>
    private async Task EnforceDeclaredRealtimeAsync(
        ServerCallContext context,
        string moduleId,
        string? eventName)
    {
        var registry = _serviceProvider.GetService<IModuleManifestRegistry>();
        var declared = registry?.GetRealtimeEvents(moduleId);

        if (declared is null)
        {
            // The supervisor records every manifest it reads before launching a host, so an
            // unrecorded module did not come through a supervised launch. Fail closed.
            _logger.LogWarning(
                "BroadcastRealtimeEvent rejected: no manifest is recorded for module {ModuleId}",
                LogSanitizer.Sanitize(moduleId));

            throw new RpcException(
                new Status(
                    StatusCode.PermissionDenied,
                    "[MODULE_EVENT_REJECTED] No manifest is recorded for this module."),
                new Metadata { { "error-code", ErrorCodes.ModuleEventRejected } });
        }

        if (declared.Count == 0
            || (!string.IsNullOrEmpty(eventName)
                && declared.Contains(eventName, StringComparer.OrdinalIgnoreCase)))
        {
            // An empty declaration means the vocabulary has not been added to that module's
            // manifest yet; it keeps working unchanged.
            return;
        }

        _logger.LogWarning(
            "BroadcastRealtimeEvent rejected for module {ModuleId}: {EventName} is not declared in the manifest",
            LogSanitizer.Sanitize(moduleId),
            LogSanitizer.Sanitize(eventName ?? string.Empty));

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            await auditLogger.LogAsync(
                new AuditEntry
                {
                    Caller = CallerSubjectGuard.GetCaller(context) ?? CallerContext.CreateSystemContext(),
                    ModuleId = moduleId,
                    Action = AuditAction.ModuleEventRejected,
                    EntityType = "ModuleEvent",
                    EntityId = Guid.Empty,
                    Description =
                        $"Real-time event '{LogSanitizer.Sanitize(eventName ?? string.Empty)}' is not " +
                        "declared in the module manifest.",
                },
                context.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not record the rejected-event audit entry for module {ModuleId}", moduleId);
        }

        throw new RpcException(
            new Status(
                StatusCode.PermissionDenied,
                "[MODULE_EVENT_REJECTED] The real-time event name is not declared in the module manifest."),
            new Metadata { { "error-code", ErrorCodes.ModuleEventRejected } });
    }

    private static string GetModuleId(ServerCallContext context)
    {
        return context.UserState.TryGetValue("ModuleId", out var mid)
            ? mid as string ?? "unknown"
            : "unknown";
    }

    /// <summary>
    /// Binds a user-scoped request to the subject the call may act for, throwing when the payload
    /// names someone other than the verified caller.
    /// </summary>
    /// <remarks>
    /// A denial is audited as <see cref="AuditAction.ModuleIdentityMismatch"/> before the call is
    /// rejected; an audit failure never changes the outcome.
    /// </remarks>
    private async Task<SubjectBinding> BindSubjectAsync(
        ServerCallContext context,
        string? requestedUserId,
        string rpc)
    {
        var binding = CallerSubjectGuard.Bind(context, requestedUserId);

        if (binding.IsAllowed)
        {
            if (binding.IsModuleScope)
            {
                _logger.LogDebug(
                    "{Rpc} served {UserId} to module {ModuleId} on a module-scope (no user assertion) call",
                    rpc, binding.UserId, GetModuleId(context));
            }

            return binding;
        }

        if (binding.Outcome is SubjectBindingOutcome.Mismatch)
        {
            await AuditSubjectMismatchAsync(context, rpc);
        }

        throw CallerSubjectGuard.CreateDeniedException(binding.Outcome);
    }

    private async Task AuditSubjectMismatchAsync(
        ServerCallContext context,
        string rpc)
    {
        var moduleId = GetModuleId(context);
        var caller = CallerSubjectGuard.GetCaller(context);

        _logger.LogWarning(
            "{Rpc} rejected for module {ModuleId}: the caller ({CallerUserId}) asked for another user's data",
            rpc,
            LogSanitizer.Sanitize(moduleId),
            caller?.UserId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            await auditLogger.LogAsync(
                new AuditEntry
                {
                    Caller = caller ?? CallerContext.CreateSystemContext(),
                    ModuleId = moduleId,
                    Action = AuditAction.ModuleIdentityMismatch,
                    EntityType = "ModuleAuth",
                    EntityId = Guid.Empty,
                    Description = $"{rpc} denied: request named a user other than the authenticated caller.",
                },
                context.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not record the subject-mismatch audit entry for module {ModuleId}", moduleId);
        }
    }

    /// <summary>
    /// Extracts the typing user's ID from a <c>TypingIndicator</c> event payload, if present.
    /// The broadcast must exclude this user's own connections so the typist never sees their
    /// own typing indicator.
    /// </summary>
    private static bool TryGetTypingUserId(string? payloadJson, out Guid userId)
    {
        userId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(payloadJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            return doc.RootElement.TryGetProperty("userId", out var userIdElement)
                && userIdElement.TryGetGuid(out userId);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
