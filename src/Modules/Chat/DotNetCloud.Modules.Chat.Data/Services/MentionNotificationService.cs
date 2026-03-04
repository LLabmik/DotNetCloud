using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IUserDirectory = DotNetCloud.Core.Capabilities.IUserDirectory;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Dispatches mention notifications to users via real-time (SignalR) and push channels.
/// Excludes the sender, respects per-channel notification preferences and mute settings.
/// </summary>
internal sealed class MentionNotificationService : IMentionNotificationService
{
    private readonly ChatDbContext _db;
    private readonly IChatRealtimeService _realtimeService;
    private readonly IPushNotificationService _pushService;
    private readonly IUserDirectory? _userDirectory;
    private readonly ILogger<MentionNotificationService> _logger;

    public MentionNotificationService(
        ChatDbContext db,
        IChatRealtimeService realtimeService,
        IPushNotificationService pushService,
        ILogger<MentionNotificationService> logger,
        IUserDirectory? userDirectory = null)
    {
        _db = db;
        _realtimeService = realtimeService;
        _pushService = pushService;
        _logger = logger;
        _userDirectory = userDirectory;
    }

    /// <inheritdoc />
    public async Task DispatchMentionNotificationsAsync(
        Guid messageId,
        Guid channelId,
        Guid senderUserId,
        IReadOnlyList<MessageMention> mentions,
        CancellationToken cancellationToken)
    {
        if (mentions.Count == 0)
            return;

        var targetUserIds = await ResolveMentionTargetsAsync(channelId, senderUserId, mentions, cancellationToken);
        if (targetUserIds.Count == 0)
            return;

        // Resolve the sender display name for notification text
        var senderName = await GetSenderDisplayNameAsync(senderUserId, cancellationToken);
        var channelName = await GetChannelNameAsync(channelId, cancellationToken);

        _logger.LogInformation(
            "Dispatching mention notifications for message {MessageId} in channel {ChannelId} to {Count} user(s)",
            messageId, channelId, targetUserIds.Count);

        foreach (var userId in targetUserIds)
        {
            // Send real-time unread/mention count update
            await _realtimeService.BroadcastUnreadCountAsync(userId, channelId, count: 1, cancellationToken);

            // Send push notification
            await _pushService.SendAsync(userId, new PushNotification
            {
                Title = $"{senderName} mentioned you in #{channelName}",
                Body = "You were mentioned in a message.",
                Category = NotificationCategory.ChatMention,
                Data = new Dictionary<string, string>
                {
                    ["channelId"] = channelId.ToString(),
                    ["messageId"] = messageId.ToString()
                }
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Resolves the set of user IDs that should receive mention notifications.
    /// Excludes the sender, muted members, and members with <see cref="NotificationPreference.None"/>.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> ResolveMentionTargetsAsync(
        Guid channelId,
        Guid senderUserId,
        IReadOnlyList<MessageMention> mentions,
        CancellationToken cancellationToken)
    {
        var hasAllOrChannelMention = mentions.Any(m =>
            m.Type == MentionType.All || m.Type == MentionType.Channel);

        if (hasAllOrChannelMention)
        {
            // @all or @channel → notify all channel members (except sender and filtered members)
            return await _db.ChannelMembers
                .AsNoTracking()
                .Where(cm => cm.ChannelId == channelId
                    && cm.UserId != senderUserId
                    && !cm.IsMuted
                    && cm.NotificationPref != NotificationPreference.None)
                .Select(cm => cm.UserId)
                .ToListAsync(cancellationToken);
        }

        // Collect individual user mentions
        var mentionedUserIds = mentions
            .Where(m => m.Type == MentionType.User && m.MentionedUserId.HasValue)
            .Select(m => m.MentionedUserId!.Value)
            .Distinct()
            .Where(id => id != senderUserId)
            .ToList();

        if (mentionedUserIds.Count == 0)
            return [];

        // Filter out muted members and those with NotificationPreference.None
        var eligibleMembers = await _db.ChannelMembers
            .AsNoTracking()
            .Where(cm => cm.ChannelId == channelId
                && mentionedUserIds.Contains(cm.UserId)
                && !cm.IsMuted
                && cm.NotificationPref != NotificationPreference.None)
            .Select(cm => cm.UserId)
            .ToListAsync(cancellationToken);

        return eligibleMembers;
    }

    private async Task<string> GetSenderDisplayNameAsync(Guid senderUserId, CancellationToken cancellationToken)
    {
        if (_userDirectory is not null)
        {
            var names = await _userDirectory.GetDisplayNamesAsync([senderUserId], cancellationToken);
            if (names.TryGetValue(senderUserId, out var displayName))
                return displayName;
        }

        return "Someone";
    }

    private async Task<string> GetChannelNameAsync(Guid channelId, CancellationToken cancellationToken)
    {
        var channel = await _db.Channels
            .AsNoTracking()
            .Where(c => c.Id == channelId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return channel ?? "unknown";
    }
}
