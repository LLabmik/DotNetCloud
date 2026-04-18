using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Handles video call events and dispatches push notifications to relevant channel members.
/// Incoming calls are sent as high-priority notifications; missed and ended calls are normal priority.
/// Uses <see cref="IServiceScopeFactory"/> to create scoped DbContext per event.
/// </summary>
internal sealed class CallNotificationEventHandler
    : ICallNotificationHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPushNotificationService _pushService;
    private readonly IUserDirectory? _userDirectory;
    private readonly ILogger<CallNotificationEventHandler> _logger;

    public CallNotificationEventHandler(
        IServiceScopeFactory scopeFactory,
        IPushNotificationService pushService,
        ILogger<CallNotificationEventHandler> logger,
        IUserDirectory? userDirectory = null)
    {
        _scopeFactory = scopeFactory;
        _pushService = pushService;
        _logger = logger;
        _userDirectory = userDirectory;
    }

    /// <summary>
    /// Sends high-priority incoming call push notifications to all channel members except the initiator.
    /// </summary>
    public async Task HandleAsync(VideoCallInitiatedEvent @event, CancellationToken cancellationToken = default)
    {
        var callerName = await GetDisplayNameAsync(@event.InitiatorUserId, cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channelName = await GetChannelNameAsync(db, @event.ChannelId, cancellationToken);
        var mediaLabel = string.Equals(@event.MediaType, "Video", StringComparison.OrdinalIgnoreCase)
            ? "video" : "audio";

        var recipientIds = await GetChannelMemberIdsExceptAsync(db, @event.ChannelId, @event.InitiatorUserId, cancellationToken);
        if (recipientIds.Count == 0)
        {
            _logger.LogDebug("No recipients for incoming call notification (CallId={CallId})", @event.CallId);
            return;
        }

        var notification = new PushNotification
        {
            Title = $"Incoming {mediaLabel} call",
            Body = @event.IsGroupCall
                ? $"{callerName} is calling in #{channelName}"
                : $"{callerName} is calling you",
            Category = NotificationCategory.IncomingCall,
            Data = new Dictionary<string, string>
            {
                ["callId"] = @event.CallId.ToString(),
                ["channelId"] = @event.ChannelId.ToString(),
                ["initiatorUserId"] = @event.InitiatorUserId.ToString(),
                ["mediaType"] = @event.MediaType,
                ["isGroupCall"] = @event.IsGroupCall.ToString(),
                ["action"] = "incoming_call"
            }
        };

        await _pushService.SendToMultipleAsync(recipientIds, notification, cancellationToken);

        _logger.LogInformation(
            "Sent incoming call push notification for call {CallId} to {RecipientCount} recipient(s)",
            @event.CallId, recipientIds.Count);
    }

    /// <summary>
    /// Sends normal-priority missed call push notifications to all channel members except the initiator.
    /// </summary>
    public async Task HandleAsync(VideoCallMissedEvent @event, CancellationToken cancellationToken = default)
    {
        var callerName = await GetDisplayNameAsync(@event.InitiatorUserId, cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var recipientIds = await GetChannelMemberIdsExceptAsync(db, @event.ChannelId, @event.InitiatorUserId, cancellationToken);
        if (recipientIds.Count == 0)
        {
            _logger.LogDebug("No recipients for missed call notification (CallId={CallId})", @event.CallId);
            return;
        }

        var notification = new PushNotification
        {
            Title = "Missed call",
            Body = $"You missed a call from {callerName}",
            Category = NotificationCategory.MissedCall,
            Data = new Dictionary<string, string>
            {
                ["callId"] = @event.CallId.ToString(),
                ["channelId"] = @event.ChannelId.ToString(),
                ["initiatorUserId"] = @event.InitiatorUserId.ToString(),
                ["action"] = "missed_call"
            }
        };

        await _pushService.SendToMultipleAsync(recipientIds, notification, cancellationToken);

        _logger.LogInformation(
            "Sent missed call push notification for call {CallId} to {RecipientCount} recipient(s)",
            @event.CallId, recipientIds.Count);
    }

    /// <summary>
    /// Sends call-ended push notifications to participants who were disconnected
    /// (left the call before it ended, i.e. they have a LeftAtUtc set).
    /// </summary>
    public async Task HandleAsync(VideoCallEndedEvent @event, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var disconnectedUserIds = await db.CallParticipants
            .AsNoTracking()
            .Where(cp => cp.VideoCallId == @event.CallId && cp.LeftAtUtc != null)
            .Select(cp => cp.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (disconnectedUserIds.Count == 0)
        {
            return;
        }

        var durationText = @event.DurationSeconds.HasValue
            ? FormatDuration(@event.DurationSeconds.Value)
            : null;

        var notification = new PushNotification
        {
            Title = "Call ended",
            Body = durationText is not null
                ? $"The call has ended (duration: {durationText})"
                : "The call has ended",
            Category = NotificationCategory.CallEnded,
            Data = new Dictionary<string, string>
            {
                ["callId"] = @event.CallId.ToString(),
                ["channelId"] = @event.ChannelId.ToString(),
                ["endReason"] = @event.EndReason,
                ["action"] = "call_ended"
            }
        };

        await _pushService.SendToMultipleAsync(disconnectedUserIds, notification, cancellationToken);

        _logger.LogInformation(
            "Sent call-ended push notification for call {CallId} to {RecipientCount} disconnected participant(s)",
            @event.CallId, disconnectedUserIds.Count);
    }

    private static async Task<IReadOnlyList<Guid>> GetChannelMemberIdsExceptAsync(
        ChatDbContext db, Guid channelId, Guid excludeUserId, CancellationToken cancellationToken)
    {
        return await db.ChannelMembers
            .AsNoTracking()
            .Where(cm => cm.ChannelId == channelId && cm.UserId != excludeUserId)
            .Select(cm => cm.UserId)
            .ToListAsync(cancellationToken);
    }

    private async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_userDirectory is not null)
        {
            var names = await _userDirectory.GetDisplayNamesAsync([userId], cancellationToken);
            if (names.TryGetValue(userId, out var displayName))
                return displayName;
        }

        return "Someone";
    }

    private static async Task<string> GetChannelNameAsync(ChatDbContext db, Guid channelId, CancellationToken cancellationToken)
    {
        var name = await db.Channels
            .AsNoTracking()
            .Where(c => c.Id == channelId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return name ?? "unknown";
    }

    internal static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 60)
            return $"{totalSeconds}s";

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;

        if (minutes < 60)
            return seconds > 0 ? $"{minutes}m {seconds}s" : $"{minutes}m";

        var hours = minutes / 60;
        minutes %= 60;
        return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
    }
}
