using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Provides real-time broadcasting of chat events to connected clients via <see cref="IRealtimeBroadcaster"/>.
/// </summary>
public interface IChatRealtimeService
{
    /// <summary>Broadcasts a new message to all members of a channel.</summary>
    Task BroadcastNewMessageAsync(Guid channelId, MessageDto message, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a message edit to all members of a channel.</summary>
    Task BroadcastMessageEditedAsync(Guid channelId, MessageDto message, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a message deletion to all members of a channel.</summary>
    Task BroadcastMessageDeletedAsync(Guid channelId, Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a typing indicator to all members of a channel.</summary>
    Task BroadcastTypingAsync(Guid channelId, Guid userId, string? displayName, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a reaction update to all members of a channel.</summary>
    Task BroadcastReactionUpdatedAsync(Guid channelId, Guid messageId, IReadOnlyList<MessageReactionDto> reactions, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a channel metadata update to all members.</summary>
    Task BroadcastChannelUpdatedAsync(ChannelDto channel, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a member joined notification.</summary>
    Task BroadcastMemberJoinedAsync(Guid channelId, ChannelMemberDto member, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a member left notification.</summary>
    Task BroadcastMemberLeftAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts updated unread counts to a specific user.</summary>
    Task BroadcastUnreadCountAsync(Guid userId, Guid channelId, int count, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a presence change to relevant channel members.</summary>
    Task BroadcastPresenceChangedAsync(PresenceDto presence, CancellationToken cancellationToken = default);

    /// <summary>Sends a channel invite notification to a specific user.</summary>
    Task SendInviteNotificationAsync(Guid userId, ChannelInviteDto invite, CancellationToken cancellationToken = default);

    /// <summary>Sends a call invite notification to a specific user (mid-call or direct).</summary>
    Task SendCallInviteAsync(
        Guid targetUserId,
        Guid callId,
        Guid channelId,
        Guid invitedByUserId,
        string? invitedByDisplayName,
        string mediaType,
        bool isMidCallInvite,
        int participantCount,
        CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a host transfer notification to all members of a channel.</summary>
    Task BroadcastHostTransferredAsync(
        Guid channelId,
        Guid callId,
        Guid previousHostUserId,
        Guid newHostUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a user to a channel's broadcast group.</summary>
    Task AddUserToChannelGroupAsync(Guid userId, Guid channelId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a channel's broadcast group.</summary>
    Task RemoveUserFromChannelGroupAsync(Guid userId, Guid channelId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="IChatRealtimeService"/> that delegates to <see cref="IRealtimeBroadcaster"/>.
/// When no broadcaster is available (module running standalone), operations are no-ops.
/// </summary>
internal sealed class ChatRealtimeService : IChatRealtimeService
{
    private readonly IRealtimeBroadcaster? _broadcaster;
    private readonly ILogger<ChatRealtimeService> _logger;

    public ChatRealtimeService(ILogger<ChatRealtimeService> logger, IRealtimeBroadcaster? broadcaster = null)
    {
        _broadcaster = broadcaster;
        _logger = logger;
    }

    private static string ChannelGroup(Guid channelId) => $"chat-channel-{channelId}";

    /// <inheritdoc />
    public async Task BroadcastNewMessageAsync(Guid channelId, MessageDto message, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(ChannelGroup(channelId), "NewMessage", new { channelId, message }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastMessageEditedAsync(Guid channelId, MessageDto message, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(ChannelGroup(channelId), "MessageEdited", new { channelId, message }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastMessageDeletedAsync(Guid channelId, Guid messageId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(ChannelGroup(channelId), "MessageDeleted", new { channelId, messageId }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastTypingAsync(Guid channelId, Guid userId, string? displayName, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(ChannelGroup(channelId), "TypingIndicator", new { channelId, userId, displayName }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastReactionUpdatedAsync(Guid channelId, Guid messageId, IReadOnlyList<MessageReactionDto> reactions, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(ChannelGroup(channelId), "ReactionUpdated", new { channelId, messageId, reactions }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastChannelUpdatedAsync(ChannelDto channel, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(ChannelGroup(channel.Id), "ChannelUpdated", channel, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastMemberJoinedAsync(Guid channelId, ChannelMemberDto member, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(ChannelGroup(channelId), "MemberJoined", new { channelId, member }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastMemberLeftAsync(Guid channelId, Guid userId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(ChannelGroup(channelId), "MemberLeft", new { channelId, userId }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastUnreadCountAsync(Guid userId, Guid channelId, int count, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.SendToUserAsync(userId, "UnreadCountUpdated", new { channelId, count }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastPresenceChangedAsync(PresenceDto presence, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        // Broadcast to all connected clients — presence is cross-channel
        await _broadcaster.BroadcastAsync("chat-presence", "PresenceChanged", presence, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendInviteNotificationAsync(Guid userId, ChannelInviteDto invite, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.SendToUserAsync(userId, "ChannelInviteReceived", invite, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendCallInviteAsync(
        Guid targetUserId,
        Guid callId,
        Guid channelId,
        Guid invitedByUserId,
        string? invitedByDisplayName,
        string mediaType,
        bool isMidCallInvite,
        int participantCount,
        CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.SendToUserAsync(targetUserId, "CallInviteReceived", new
        {
            callId,
            channelId,
            invitedByUserId,
            invitedByDisplayName,
            mediaType,
            isMidCallInvite,
            participantCount
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddUserToChannelGroupAsync(Guid userId, Guid channelId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.AddToGroupAsync(userId, ChannelGroup(channelId), cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastHostTransferredAsync(
        Guid channelId,
        Guid callId,
        Guid previousHostUserId,
        Guid newHostUserId,
        CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(ChannelGroup(channelId), "HostTransferred", new
        {
            callId,
            previousHostUserId,
            newHostUserId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveUserFromChannelGroupAsync(Guid userId, Guid channelId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.RemoveFromGroupAsync(userId, ChannelGroup(channelId), cancellationToken);
    }
}
