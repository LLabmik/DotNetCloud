using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Services;
using Grpc.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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

    public CoreCapabilitiesServiceImpl(
        ILogger<CoreCapabilitiesServiceImpl> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Looks up user information by ID (IUserDirectory capability).
    /// </summary>
    public override Task<GetUserResponse> GetUser(GetUserRequest request, ServerCallContext context)
    {
        _logger.LogDebug("GetUser called for {UserId} by module {ModuleId}",
            request.UserId, GetModuleId(context));

        // Placeholder: will be wired to IUserDirectory implementation
        return Task.FromResult(new GetUserResponse { Found = false });
    }

    /// <summary>
    /// Searches users matching criteria (IUserDirectory capability).
    /// </summary>
    public override Task<SearchUsersResponse> SearchUsers(SearchUsersRequest request, ServerCallContext context)
    {
        _logger.LogDebug("SearchUsers called with query '{Query}' by module {ModuleId}",
            request.Query, GetModuleId(context));

        return Task.FromResult(new SearchUsersResponse());
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
    public override Task<SendNotificationResponse> SendNotification(SendNotificationRequest request, ServerCallContext context)
    {
        _logger.LogInformation(
            "SendNotification: '{Title}' to {Count} recipients from module {ModuleId}",
            request.Title, request.RecipientUserIds.Count, GetModuleId(context));

        // Placeholder: will be wired to INotificationService implementation
        return Task.FromResult(new SendNotificationResponse
        {
            Success = true,
            DeliveredCount = request.RecipientUserIds.Count
        });
    }

    /// <summary>
    /// Publishes an event on the event bus (IEventBus capability).
    /// </summary>
    public override Task<PublishEventResponse> PublishEvent(PublishEventRequest request, ServerCallContext context)
    {
        _logger.LogDebug("PublishEvent: {EventType} from module {ModuleId}",
            request.EventType, GetModuleId(context));

        // Placeholder: will be wired to IEventBus implementation
        return Task.FromResult(new PublishEventResponse { Success = true });
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
            }
            else
            {
                // Group broadcast
                object? payload = null;
                if (!string.IsNullOrEmpty(request.PayloadJson))
                    payload = JsonSerializer.Deserialize<object>(request.PayloadJson);

                await broadcaster.BroadcastAsync(request.Group, request.EventName, payload ?? request.PayloadJson, context.CancellationToken);
            }

            return new BroadcastRealtimeEventResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BroadcastRealtimeEvent failed for group={Group}, event={Event} from module {ModuleId}",
                request.Group, request.EventName, moduleId);
            return new BroadcastRealtimeEventResponse { Success = false };
        }
    }

    private static string GetModuleId(ServerCallContext context)
    {
        return context.UserState.TryGetValue("ModuleId", out var mid)
            ? mid as string ?? "unknown"
            : "unknown";
    }
}
