using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Modules.Supervisor;
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
internal sealed class CoreCapabilitiesServiceImpl : CoreCapabilities.CoreCapabilitiesBase
{
    private readonly ILogger<CoreCapabilitiesServiceImpl> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SearchIndexingService _indexingService;
    private readonly ModuleEventInbox _eventInbox;

    public CoreCapabilitiesServiceImpl(
        ILogger<CoreCapabilitiesServiceImpl> logger,
        IServiceProvider serviceProvider,
        SearchIndexingService indexingService,
        ModuleEventInbox eventInbox)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _indexingService = indexingService;
        _eventInbox = eventInbox;
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
    public override async Task<SearchUsersResponse> SearchUsers(SearchUsersRequest request, ServerCallContext context)
    {
        _logger.LogDebug("SearchUsers called with query '{Query}' by module {ModuleId}",
            request.Query, GetModuleId(context));

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var userDir = scope.ServiceProvider.GetRequiredService<IUserDirectory>();

            var results = await userDir.SearchUsersAsync(request.Query, request.MaxResults > 0 ? request.MaxResults : 20);

            var response = new SearchUsersResponse();
            response.Users.AddRange(results.Select(r => new UserInfo
            {
                Id = r.Id.ToString(),
                DisplayName = r.DisplayName,
                Email = r.Email,
            }));

            _logger.LogDebug("SearchUsers: found {Count} results for query '{Query}'", response.Users.Count, request.Query);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchUsers failed for query '{Query}'", request.Query);
            return new SearchUsersResponse();
        }
    }

    /// <summary>
    /// Gets the current caller's identity (ICurrentUserContext capability).
    /// </summary>
    public override Task<GetCurrentUserResponse> GetCurrentUser(GetCurrentUserRequest request, ServerCallContext context)
    {
        _logger.LogDebug("GetCurrentUser called by module {ModuleId}", GetModuleId(context));

        var response = new GetCurrentUserResponse
        {
            UserId = request.Caller?.UserId ?? string.Empty,
            DisplayName = string.Empty,
            Email = string.Empty,
            Locale = "en-US",
            Timezone = "UTC"
        };

        return Task.FromResult(response);
    }

    /// <summary>
    /// Sends a notification (INotificationService capability).
    /// </summary>
    public override async Task<SendNotificationResponse> SendNotification(SendNotificationRequest request, ServerCallContext context)
    {
        var moduleId = GetModuleId(context);
        _logger.LogInformation(
            "SendNotification: '{Title}' to {Count} recipients from module {ModuleId}",
            request.Title, request.RecipientUserIds.Count, moduleId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            // The category the caller supplied decides how the notification is stored. It used to be
            // accepted and then dropped, so every module-sent notification — calendar reminders
            // included — was persisted as a generic Info/Normal entry.
            var (type, priority) = MapNotificationCategory(request.Category);

            var deliveredCount = 0;

            foreach (var rawUserId in request.RecipientUserIds)
            {
                if (!Guid.TryParse(rawUserId, out var userId))
                    continue;

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
    /// Maps a notification category supplied by a calling module onto the notification type and
    /// priority to store.
    /// </summary>
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
    public override async Task<SubmitSearchIndexResponse> SubmitSearchIndex(
        SubmitSearchIndexRequest request, ServerCallContext context)
    {
        if (request.Action is < 0 or > 1)
        {
            _logger.LogWarning(
                "SubmitSearchIndex: invalid action {Action} for {ModuleId}/{EntityId} from module {Caller}",
                request.Action, request.ModuleId, request.EntityId, GetModuleId(context));
            return new SubmitSearchIndexResponse { Success = false };
        }

        await _indexingService.EnqueueAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = request.ModuleId,
            EntityId = request.EntityId,
            Action = (SearchIndexAction)request.Action
        });

        return new SubmitSearchIndexResponse { Success = true };
    }

    /// <summary>
    /// Gets a module setting value (IModuleSettings capability).
    /// </summary>
    public override Task<GetSettingResponse> GetSetting(GetSettingRequest request, ServerCallContext context)
    {
        _logger.LogDebug("GetSetting: {Module}/{Key} by module {ModuleId}",
            request.ModuleId, request.Key, GetModuleId(context));

        return Task.FromResult(new GetSettingResponse { Found = false });
    }

    /// <summary>
    /// Sets a module setting value (IModuleSettings capability).
    /// </summary>
    public override Task<SetSettingResponse> SetSetting(SetSettingRequest request, ServerCallContext context)
    {
        _logger.LogDebug("SetSetting: {Module}/{Key} by module {ModuleId}",
            request.ModuleId, request.Key, GetModuleId(context));

        return Task.FromResult(new SetSettingResponse { Success = true });
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
    public override async Task<GetTeamsForUserResponse> GetTeamsForUser(GetTeamsForUserRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            _logger.LogWarning("GetTeamsForUser called with invalid UserId '{RawId}'", LogSanitizer.Sanitize(request.UserId));
            return new GetTeamsForUserResponse();
        }

        _logger.LogInformation("GetTeamsForUser called for {UserId} by module {ModuleId}",
            userId, GetModuleId(context));

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
    /// Lists the groups a user belongs to (IGroupDirectory capability). Module hosts use this to
    /// resolve admin shared-folder group grants; without it a host cannot tell whether a caller
    /// is entitled to a folder, and must deny by default.
    /// </summary>
    public override async Task<GetGroupsForUserResponse> GetGroupsForUser(
        GetGroupsForUserRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            _logger.LogWarning("GetGroupsForUser called with invalid UserId '{RawId}'", LogSanitizer.Sanitize(request.UserId));
            return new GetGroupsForUserResponse();
        }

        _logger.LogDebug("GetGroupsForUser called for {UserId} by module {ModuleId}",
            userId, GetModuleId(context));

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
    public override async Task<BroadcastRealtimeEventResponse> BroadcastRealtimeEvent(
        BroadcastRealtimeEventRequest request, ServerCallContext context)
    {
        var moduleId = GetModuleId(context);
        _logger.LogDebug(
            "BroadcastRealtimeEvent: group={Group}, event={Event} from module {ModuleId}",
            request.Group, request.EventName, moduleId);

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
                ModuleId = string.IsNullOrWhiteSpace(request.ModuleId) ? moduleId : request.ModuleId,
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

    private static string GetModuleId(ServerCallContext context)
    {
        return context.UserState.TryGetValue("ModuleId", out var mid)
            ? mid as string ?? "unknown"
            : "unknown";
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
