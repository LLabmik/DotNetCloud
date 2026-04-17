using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.Configuration;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// The primary SignalR hub for the DotNetCloud platform.
/// Manages real-time client connections, presence tracking, and group membership.
/// All clients must be authenticated to connect.
/// Accepts both Identity cookie auth (Blazor UI) and OpenIddict bearer tokens (mobile clients).
/// </summary>
[Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]
internal sealed class CoreHub : Hub
{
    private readonly UserConnectionTracker _connectionTracker;
    private readonly PresenceService _presenceService;
    private readonly IMessageService? _messageService;
    private readonly IChannelMemberService? _channelMemberService;
    private readonly IReactionService? _reactionService;
    private readonly ITypingIndicatorService? _typingIndicatorService;
    private readonly IChatRealtimeService? _chatRealtimeService;
    private readonly ICallSignalingService? _callSignalingService;
    private readonly IVideoCallService? _videoCallService;
    private readonly IEventBus? _eventBus;
    private readonly ILogger<CoreHub> _logger;

    public CoreHub(
        UserConnectionTracker connectionTracker,
        PresenceService presenceService,
        IMessageService? messageService,
        IChannelMemberService? channelMemberService,
        IReactionService? reactionService,
        ITypingIndicatorService? typingIndicatorService,
        IChatRealtimeService? chatRealtimeService,
        ILogger<CoreHub> logger,
        ICallSignalingService? callSignalingService = null,
        IVideoCallService? videoCallService = null,
        IEventBus? eventBus = null)
    {
        _connectionTracker = connectionTracker;
        _presenceService = presenceService;
        _messageService = messageService;
        _channelMemberService = channelMemberService;
        _reactionService = reactionService;
        _typingIndicatorService = typingIndicatorService;
        _chatRealtimeService = chatRealtimeService;
        _callSignalingService = callSignalingService;
        _videoCallService = videoCallService;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        var isFirstConnection = _connectionTracker.AddConnection(userId, connectionId);

        _logger.LogInformation(
            "User {UserId} connected with connection {ConnectionId} (first: {IsFirst})",
            userId, connectionId, isFirstConnection);

        var trackedGroups = _connectionTracker.GetGroups(userId);
        foreach (var group in trackedGroups)
        {
            await Groups.AddToGroupAsync(connectionId, group);
        }

        // Auto-join the cross-module tracks-activity broadcast group so all
        // connected users receive live Tracks ↔ Chat integration events.
        await Groups.AddToGroupAsync(connectionId, "tracks-activity");

        if (isFirstConnection)
        {
            await _presenceService.UserConnectedAsync(userId, connectionId);

            // Notify other clients that this user is now online
            await Clients.Others.SendAsync("UserOnline", new { UserId = userId, Timestamp = DateTime.UtcNow });
        }

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var result = _connectionTracker.RemoveConnection(connectionId);

        if (result is not null)
        {
            var (userId, isLastConnection) = result.Value;

            _logger.LogInformation(
                "User {UserId} disconnected connection {ConnectionId} (last: {IsLast})",
                userId, connectionId, isLastConnection);

            if (isLastConnection)
            {
                await _presenceService.UserDisconnectedAsync(userId, connectionId);

                // Notify other clients that this user is now offline
                await Clients.Others.SendAsync("UserOffline", new { UserId = userId, Timestamp = DateTime.UtcNow });
            }
        }

        if (exception is not null)
        {
            _logger.LogWarning(exception, "Connection {ConnectionId} disconnected with error", connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins the calling user to a channel group after verifying membership.
    /// Only users who are members of the channel are allowed to join.
    /// </summary>
    /// <param name="channelId">The channel ID to join.</param>
    public async Task JoinGroupAsync(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            throw new HubException("Channel ID cannot be empty.");
        }

        if (!Guid.TryParse(channelId, out var parsedChannelId))
        {
            throw new HubException("Invalid channel ID format.");
        }

        // Verify the user is a member of this channel before allowing group join
        if (_channelMemberService is not null)
        {
            var caller = CreateUserCaller();
            var isMember = await _channelMemberService.IsMemberAsync(
                parsedChannelId, caller, Context.ConnectionAborted);

            if (!isMember)
            {
                _logger.LogWarning(
                    "User {UserId} denied group join for channel {ChannelId} — not a member",
                    GetUserId(), channelId);
                throw new HubException("You are not a member of this channel.");
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, channelId);
        _connectionTracker.AddGroupMembership(GetUserId(), channelId);

        _logger.LogDebug(
            "User {UserId} joined group {Group} via connection {ConnectionId}",
            GetUserId(), channelId, Context.ConnectionId);
    }

    /// <summary>
    /// Removes the calling user from a channel group.
    /// </summary>
    /// <param name="channelId">The channel ID to leave.</param>
    public async Task LeaveGroupAsync(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            throw new HubException("Channel ID cannot be empty.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId);
        _connectionTracker.RemoveGroupMembership(GetUserId(), channelId);

        _logger.LogDebug(
            "User {UserId} left group {Group} via connection {ConnectionId}",
            GetUserId(), channelId, Context.ConnectionId);
    }

    /// <summary>
    /// Joins the calling user to a board-scoped chat group for receiving
    /// Tracks ↔ Chat integration events scoped to a specific board.
    /// </summary>
    /// <param name="boardId">The board ID to subscribe to chat activity for.</param>
    public async Task JoinBoardChatGroupAsync(string boardId)
    {
        if (string.IsNullOrWhiteSpace(boardId))
        {
            throw new HubException("Board ID cannot be empty.");
        }

        if (!Guid.TryParse(boardId, out _))
        {
            throw new HubException("Invalid board ID format.");
        }

        var groupName = $"tracks-board-chat-{boardId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _connectionTracker.AddGroupMembership(GetUserId(), groupName);

        _logger.LogDebug(
            "User {UserId} joined board-chat group {Group} via connection {ConnectionId}",
            GetUserId(), groupName, Context.ConnectionId);
    }

    /// <summary>
    /// Removes the calling user from a board-scoped chat group.
    /// </summary>
    /// <param name="boardId">The board ID to unsubscribe from.</param>
    public async Task LeaveBoardChatGroupAsync(string boardId)
    {
        if (string.IsNullOrWhiteSpace(boardId))
        {
            throw new HubException("Board ID cannot be empty.");
        }

        var groupName = $"tracks-board-chat-{boardId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _connectionTracker.RemoveGroupMembership(GetUserId(), groupName);

        _logger.LogDebug(
            "User {UserId} left board-chat group {Group} via connection {ConnectionId}",
            GetUserId(), groupName, Context.ConnectionId);
    }

    /// <summary>
    /// Pings the server to keep the connection alive and update presence.
    /// Clients can call this periodically to signal activity.
    /// </summary>
    public async Task PingAsync()
    {
        var userId = GetUserId();
        await _presenceService.UpdateLastSeenAsync(userId);
    }

    /// <summary>
    /// Sends a new message to a channel and broadcasts it to channel members.
    /// </summary>
    public async Task<MessageDto> SendMessageAsync(Guid channelId, string content, Guid? replyToId = null)
    {
        EnsureChatServicesAvailable();

        try
        {
            var message = await _messageService!.SendMessageAsync(
                channelId,
                new SendMessageDto { Content = content, ReplyToMessageId = replyToId },
                CreateUserCaller(),
                Context.ConnectionAborted);

            await _chatRealtimeService!.BroadcastNewMessageAsync(channelId, message, Context.ConnectionAborted);
            return message;
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Edits an existing message and broadcasts the update to channel members.
    /// </summary>
    public async Task<MessageDto> EditMessageAsync(Guid messageId, string newContent)
    {
        EnsureChatServicesAvailable();

        try
        {
            var message = await _messageService!.EditMessageAsync(
                messageId,
                new EditMessageDto { Content = newContent },
                CreateUserCaller(),
                Context.ConnectionAborted);

            await _chatRealtimeService!.BroadcastMessageEditedAsync(message.ChannelId, message, Context.ConnectionAborted);
            return message;
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Deletes a message and broadcasts deletion to channel members.
    /// </summary>
    public async Task DeleteMessageAsync(Guid messageId)
    {
        EnsureChatServicesAvailable();

        try
        {
            var caller = CreateUserCaller();
            var message = await _messageService!.GetMessageAsync(messageId, caller, Context.ConnectionAborted)
                ?? throw new InvalidOperationException($"Message {messageId} not found.");

            await _messageService.DeleteMessageAsync(messageId, caller, Context.ConnectionAborted);
            await _chatRealtimeService!.BroadcastMessageDeletedAsync(message.ChannelId, messageId, Context.ConnectionAborted);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Signals typing activity in a channel.
    /// </summary>
    public async Task StartTypingAsync(Guid channelId, string? displayName = null)
    {
        EnsureChatServicesAvailable();

        try
        {
            var caller = CreateUserCaller();
            await _typingIndicatorService!.NotifyTypingAsync(channelId, caller, Context.ConnectionAborted);
            await _chatRealtimeService!.BroadcastTypingAsync(channelId, caller.UserId, displayName, Context.ConnectionAborted);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Signals that typing has stopped for a channel.
    /// </summary>
    public async Task StopTypingAsync(Guid channelId)
    {
        EnsureChatServicesAvailable();

        var caller = CreateUserCaller();
        await _chatRealtimeService!.BroadcastTypingAsync(channelId, caller.UserId, displayName: null, Context.ConnectionAborted);
    }

    /// <summary>
    /// Marks a channel as read up to a specific message and pushes unread count update to the caller.
    /// </summary>
    public async Task MarkReadAsync(Guid channelId, Guid messageId)
    {
        EnsureChatServicesAvailable();

        try
        {
            var caller = CreateUserCaller();
            await _channelMemberService!.MarkAsReadAsync(channelId, messageId, caller, Context.ConnectionAborted);

            var unread = await _channelMemberService.GetUnreadCountsAsync(caller, Context.ConnectionAborted);
            var count = unread.FirstOrDefault(x => x.ChannelId == channelId)?.UnreadCount ?? 0;

            await _chatRealtimeService!.BroadcastUnreadCountAsync(caller.UserId, channelId, count, Context.ConnectionAborted);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Adds a reaction to a message and broadcasts the updated reaction set.
    /// </summary>
    public async Task AddReactionAsync(Guid messageId, string emoji)
    {
        EnsureChatServicesAvailable();

        try
        {
            var caller = CreateUserCaller();
            await _reactionService!.AddReactionAsync(messageId, emoji, caller, Context.ConnectionAborted);

            var message = await _messageService!.GetMessageAsync(messageId, caller, Context.ConnectionAborted)
                ?? throw new InvalidOperationException($"Message {messageId} not found.");
            var reactions = await _reactionService.GetReactionsAsync(messageId, Context.ConnectionAborted);

            await _chatRealtimeService!.BroadcastReactionUpdatedAsync(message.ChannelId, messageId, reactions, Context.ConnectionAborted);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Removes a reaction from a message and broadcasts the updated reaction set.
    /// </summary>
    public async Task RemoveReactionAsync(Guid messageId, string emoji)
    {
        EnsureChatServicesAvailable();

        try
        {
            var caller = CreateUserCaller();
            await _reactionService!.RemoveReactionAsync(messageId, emoji, caller, Context.ConnectionAborted);

            var message = await _messageService!.GetMessageAsync(messageId, caller, Context.ConnectionAborted)
                ?? throw new InvalidOperationException($"Message {messageId} not found.");
            var reactions = await _reactionService.GetReactionsAsync(messageId, Context.ConnectionAborted);

            await _chatRealtimeService!.BroadcastReactionUpdatedAsync(message.ChannelId, messageId, reactions, Context.ConnectionAborted);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Updates the caller's presence status and optional custom status message.
    /// </summary>
    public async Task<PresenceDto> SetPresenceAsync(string status, string? statusMessage = null)
    {
        EnsureChatServicesAvailable();

        try
        {
            var caller = CreateUserCaller();
            var presence = await _presenceService.SetPresenceAsync(caller.UserId, status, statusMessage);

            await _chatRealtimeService!.BroadcastPresenceChangedAsync(presence, Context.ConnectionAborted);

            if (_eventBus is not null)
            {
                await _eventBus.PublishAsync(new PresenceChangedEvent
                {
                    EventId = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    UserId = presence.UserId,
                    Status = presence.Status,
                    StatusMessage = presence.StatusMessage,
                    LastSeenAt = presence.LastSeenAt
                }, caller, Context.ConnectionAborted);
            }

            return presence;
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    // ── Video Call Signaling ────────────────────────────────────────────

    /// <summary>
    /// Relays an SDP offer to a target participant for WebRTC peer connection establishment.
    /// </summary>
    /// <param name="callId">The video call ID.</param>
    /// <param name="targetUserId">The user to send the offer to.</param>
    /// <param name="sdpOffer">The SDP offer payload.</param>
    public async Task SendCallOfferAsync(Guid callId, Guid targetUserId, string sdpOffer)
    {
        EnsureCallSignalingAvailable();

        try
        {
            var caller = CreateUserCaller();
            await _callSignalingService!.SendOfferAsync(
                callId, targetUserId, sdpOffer, caller, Context.ConnectionAborted);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Relays an SDP answer back to the caller who sent the offer.
    /// </summary>
    /// <param name="callId">The video call ID.</param>
    /// <param name="targetUserId">The user to send the answer to.</param>
    /// <param name="sdpAnswer">The SDP answer payload.</param>
    public async Task SendCallAnswerAsync(Guid callId, Guid targetUserId, string sdpAnswer)
    {
        EnsureCallSignalingAvailable();

        try
        {
            var caller = CreateUserCaller();
            await _callSignalingService!.SendAnswerAsync(
                callId, targetUserId, sdpAnswer, caller, Context.ConnectionAborted);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Relays an ICE candidate to a target participant for NAT traversal.
    /// </summary>
    /// <param name="callId">The video call ID.</param>
    /// <param name="targetUserId">The user to send the candidate to.</param>
    /// <param name="candidate">The ICE candidate payload.</param>
    public async Task SendIceCandidateAsync(Guid callId, Guid targetUserId, string candidate)
    {
        EnsureCallSignalingAvailable();

        try
        {
            var caller = CreateUserCaller();
            await _callSignalingService!.SendIceCandidateAsync(
                callId, targetUserId, candidate, caller, Context.ConnectionAborted);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Notifies call participants of a media state change (mute/unmute, camera on/off, screen share).
    /// </summary>
    /// <param name="callId">The video call ID.</param>
    /// <param name="mediaType">The media type that changed (Audio, Video, ScreenShare).</param>
    /// <param name="enabled">Whether the media is now enabled.</param>
    public async Task SendMediaStateChangeAsync(Guid callId, string mediaType, bool enabled)
    {
        EnsureCallSignalingAvailable();

        try
        {
            var caller = CreateUserCaller();
            await _callSignalingService!.SendMediaStateChangeAsync(
                callId, mediaType, enabled, caller, Context.ConnectionAborted);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Joins the caller's connection to a call-scoped SignalR group for receiving broadcast signals.
    /// </summary>
    /// <param name="callId">The video call ID.</param>
    public async Task JoinCallGroupAsync(Guid callId)
    {
        var groupName = $"call-{callId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _connectionTracker.AddGroupMembership(GetUserId(), groupName);

        _logger.LogDebug(
            "User {UserId} joined call group {Group} via connection {ConnectionId}",
            GetUserId(), groupName, Context.ConnectionId);
    }

    /// <summary>
    /// Removes the caller's connection from a call-scoped SignalR group.
    /// </summary>
    /// <param name="callId">The video call ID.</param>
    public async Task LeaveCallGroupAsync(Guid callId)
    {
        var groupName = $"call-{callId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _connectionTracker.RemoveGroupMembership(GetUserId(), groupName);

        _logger.LogDebug(
            "User {UserId} left call group {Group} via connection {ConnectionId}",
            GetUserId(), groupName, Context.ConnectionId);
    }

    // ── Video Call Management (Host / Invite) ───────────────────

    /// <summary>
    /// Invites a user to join an active call. Only the call Host may invite participants.
    /// Relays to <see cref="IVideoCallService.InviteToCallAsync"/>.
    /// </summary>
    /// <param name="callId">The active call to invite the user to.</param>
    /// <param name="targetUserId">The user to invite.</param>
    public async Task InviteToCallAsync(Guid callId, Guid targetUserId)
    {
        EnsureVideoCallServiceAvailable();

        try
        {
            var caller = CreateUserCaller();
            await _videoCallService!.InviteToCallAsync(callId, targetUserId, caller, Context.ConnectionAborted);

            _logger.LogInformation(
                "User {UserId} invited {TargetUserId} to call {CallId}",
                caller.UserId, targetUserId, callId);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Transfers the host role of an active call to another participant.
    /// Only the current host may transfer. Relays to <see cref="IVideoCallService.TransferHostAsync"/>.
    /// </summary>
    /// <param name="callId">The active call.</param>
    /// <param name="newHostUserId">The participant to become the new host.</param>
    public async Task TransferHostAsync(Guid callId, Guid newHostUserId)
    {
        EnsureVideoCallServiceAvailable();

        try
        {
            var caller = CreateUserCaller();
            await _videoCallService!.TransferHostAsync(callId, newHostUserId, caller, Context.ConnectionAborted);

            _logger.LogInformation(
                "User {UserId} transferred host of call {CallId} to {NewHostUserId}",
                caller.UserId, callId, newHostUserId);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    private Guid GetUserId()
    {
        var nameIdentifier = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(nameIdentifier) || !Guid.TryParse(nameIdentifier, out var userId))
        {
            throw new HubException("Unable to determine authenticated user identity.");
        }

        return userId;
    }

    private CallerContext CreateUserCaller()
        => new(GetUserId(), ["user"], CallerType.User);

    private static bool TryConvertToHubException(Exception ex, out HubException hubException)
    {
        if (ex is UnauthorizedAccessException)
        {
            hubException = new HubException("Access denied.");
            return true;
        }

        if (ex is ArgumentException)
        {
            hubException = new HubException("Invalid request parameters.");
            return true;
        }

        if (ex is InvalidOperationException)
        {
            // Only pass through safe, expected messages (e.g., "not found").
            // Avoid leaking internal details from unexpected InvalidOperationExceptions.
            hubException = new HubException("The requested operation could not be completed.");
            return true;
        }

        hubException = null!;
        return false;
    }

    private void EnsureChatServicesAvailable()
    {
        if (_messageService is null
            || _channelMemberService is null
            || _reactionService is null
            || _typingIndicatorService is null
            || _chatRealtimeService is null)
        {
            throw new HubException("Chat services are not available.");
        }
    }

    private void EnsureCallSignalingAvailable()
    {
        if (_callSignalingService is null)
        {
            throw new HubException("Call signaling services are not available.");
        }
    }

    private void EnsureVideoCallServiceAvailable()
    {
        if (_videoCallService is null)
        {
            throw new HubException("Video call services are not available.");
        }
    }
}
