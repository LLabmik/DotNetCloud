using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Modules.Supervisor;
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

    private static string GetModuleId(ServerCallContext context)
    {
        return context.UserState.TryGetValue("ModuleId", out var mid)
            ? mid as string ?? "unknown"
            : "unknown";
    }
}
