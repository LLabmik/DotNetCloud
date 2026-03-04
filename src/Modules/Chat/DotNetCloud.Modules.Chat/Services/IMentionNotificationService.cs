using DotNetCloud.Modules.Chat.Models;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Dispatches notifications to users who are @mentioned in chat messages.
/// Handles User, Channel, and All mention types, respects notification preferences,
/// and routes through both real-time (SignalR) and push notification channels.
/// </summary>
public interface IMentionNotificationService
{
    /// <summary>
    /// Dispatches mention notifications for a message to all relevant users.
    /// Excludes the sender and respects per-channel notification preferences.
    /// </summary>
    /// <param name="messageId">The ID of the message containing mentions.</param>
    /// <param name="channelId">The channel the message was sent in.</param>
    /// <param name="senderUserId">The user who sent the message (excluded from notifications).</param>
    /// <param name="mentions">The parsed mentions from the message content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DispatchMentionNotificationsAsync(
        Guid messageId,
        Guid channelId,
        Guid senderUserId,
        IReadOnlyList<MessageMention> mentions,
        CancellationToken cancellationToken = default);
}
