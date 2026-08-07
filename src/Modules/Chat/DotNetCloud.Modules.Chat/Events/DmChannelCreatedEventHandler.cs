using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Handles <see cref="ChannelCreatedEvent"/> for DirectMessage channels.
/// Sends push notifications to the target user and raises in-process DM notification events.
/// Works alongside <see cref="ChannelCreatedEventHandler"/> which handles sidebar updates for all channels.
/// </summary>
public sealed class DmChannelCreatedEventHandler : IEventHandler<ChannelCreatedEvent>
{
    private readonly IChannelMemberService _memberService;
    private readonly IPushNotificationService _pushService;
    private readonly IChatMessageNotifier _notifier;
    private readonly IUserDirectory _userDirectory;
    private readonly ILogger<DmChannelCreatedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmChannelCreatedEventHandler"/> class.
    /// </summary>
    public DmChannelCreatedEventHandler(
        IChannelMemberService memberService,
        IPushNotificationService pushService,
        IChatMessageNotifier notifier,
        IUserDirectory userDirectory,
        ILogger<DmChannelCreatedEventHandler> logger)
    {
        _memberService = memberService;
        _pushService = pushService;
        _notifier = notifier;
        _userDirectory = userDirectory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ChannelCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        // Only handle DirectMessage channels
        if (!string.Equals(@event.ChannelType, "DirectMessage", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogInformation(
            "DM channel created: {ChannelName} (id={ChannelId}) by user {UserId}",
            @event.ChannelName, @event.ChannelId, @event.CreatedByUserId);

        // Resolve the target user (the other member who didn't create the channel)
        var caller = new Core.Authorization.CallerContext(
            @event.CreatedByUserId, ["user"], Core.Authorization.CallerType.User);

        var members = await _memberService.ListMembersAsync(@event.ChannelId, caller, cancellationToken);

        var targetUserId = members
            .Select(m => m.UserId)
            .FirstOrDefault(uid => uid != @event.CreatedByUserId);

        if (targetUserId == Guid.Empty)
        {
            _logger.LogWarning(
                "DM channel {ChannelId} has no target member other than creator {CreatorId}; skipping DM notification",
                @event.ChannelId, @event.CreatedByUserId);
            return;
        }

        // Resolve initiator display name
        var names = await _userDirectory.GetDisplayNamesAsync(
            [@event.CreatedByUserId], cancellationToken);
        var initiatorName = names.TryGetValue(@event.CreatedByUserId, out var name)
            ? name
            : @event.CreatedByUserId.ToString()[..8];

        // Send push notification to the target user
        var pushNotification = new PushNotification
        {
            Title = $"{initiatorName} started a chat with you",
            Body = "Tap to reply or manage this request",
            Category = NotificationCategory.DmChannelCreated,
            Data = new Dictionary<string, string>
            {
                ["type"] = "dm_channel_created",
                ["channelId"] = @event.ChannelId.ToString(),
                ["channelName"] = @event.ChannelName,
                ["initiatorUserId"] = @event.CreatedByUserId.ToString(),
                ["initiatorName"] = initiatorName,
            }
        };

        await _pushService.SendAsync(targetUserId, pushNotification, cancellationToken);

        _logger.LogInformation(
            "DM push notification sent to user {TargetUserId} for channel {ChannelId}",
            targetUserId, @event.ChannelId);

        // Raise in-process notification for connected Blazor clients
        _notifier.NotifyDmChannelCreated(new DmChannelCreatedNotification(
            ChannelId: @event.ChannelId,
            ChannelName: @event.ChannelName,
            InitiatorUserId: @event.CreatedByUserId,
            InitiatorDisplayName: initiatorName,
            TargetUserId: targetUserId));
    }
}
