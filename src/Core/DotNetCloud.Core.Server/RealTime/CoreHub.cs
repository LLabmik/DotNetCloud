using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.Configuration;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using EventBus = DotNetCloud.Core.Events.IEventBus;

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
    private readonly IChatApiClient _chatApiClient;
    private readonly IRealtimeBroadcaster _broadcaster;
    private readonly EventBus? _eventBus;
    private readonly ILogger<CoreHub> _logger;

    public CoreHub(
        UserConnectionTracker connectionTracker,
        PresenceService presenceService,
        IChatApiClient chatApiClient,
        IRealtimeBroadcaster broadcaster,
        ILogger<CoreHub> logger,
        EventBus? eventBus = null)
    {
        _connectionTracker = connectionTracker ?? throw new ArgumentNullException(nameof(connectionTracker));
        _presenceService = presenceService ?? throw new ArgumentNullException(nameof(presenceService));
        _chatApiClient = chatApiClient ?? throw new ArgumentNullException(nameof(chatApiClient));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _eventBus = eventBus;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    /// <summary>
    /// Channel group name prefix used to scope broadcasts to a specific channel.
    /// Must match <c>ChatHub.ChannelGroup()</c> so joins and broadcasts use the same key.
    /// </summary>
    private static string ChannelGroup(Guid channelId) => $"chat-channel-{channelId}";

    public async Task JoinGroupAsync(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new HubException("Group name cannot be empty.");
        }

        // Extract the channelId GUID from the group name.
        // Clients send the full group name ("chat-channel-{guid}") which must match
        // the broadcast group name used by ChatHub.ChannelGroup().
        Guid parsedChannelId;

        if (groupName.StartsWith("chat-channel-", StringComparison.OrdinalIgnoreCase))
        {
            var guidPart = groupName["chat-channel-".Length..];
            if (!Guid.TryParse(guidPart, out parsedChannelId))
            {
                throw new HubException("Invalid channel ID in group name.");
            }
        }
        else if (!Guid.TryParse(groupName, out parsedChannelId))
        {
            throw new HubException("Invalid group name format.");
        }

        // Verify the user is a member of this channel before allowing group join
        var userId = GetUserId();
        var isMember = await _chatApiClient.IsChannelMemberAsync(
            parsedChannelId, userId, Context.ConnectionAborted);

        if (!isMember)
        {
            _logger.LogWarning(
                "User {UserId} denied group join for channel {ChannelId} — not a member",
                userId, groupName);
            throw new HubException("You are not a member of this channel.");
        }

        // Use the same group name format as ChatHub.ChannelGroup() so broadcasts
        // to "chat-channel-{guid}" reach clients that joined via this method.
        var groupKey = ChannelGroup(parsedChannelId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupKey);
        _connectionTracker.AddGroupMembership(GetUserId(), groupKey);

        _logger.LogDebug(
            "User {UserId} joined group {Group} via connection {ConnectionId}",
            GetUserId(), groupKey, Context.ConnectionId);
    }

    /// <summary>
    /// Removes the calling user from a channel group.
    /// </summary>
    /// <param name="groupName">The group name or channel ID to leave.</param>
    public async Task LeaveGroupAsync(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new HubException("Group name cannot be empty.");
        }

        // Derive the canonical group key from the provided name.
        var groupKey = groupName.StartsWith("chat-channel-", StringComparison.OrdinalIgnoreCase)
            ? groupName
            : $"chat-channel-{groupName}";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupKey);
        _connectionTracker.RemoveGroupMembership(GetUserId(), groupKey);

        _logger.LogDebug(
            "User {UserId} left group {Group} via connection {ConnectionId}",
            GetUserId(), groupKey, Context.ConnectionId);
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
    /// Updates the caller's presence status and optional custom status message.
    /// Broadcasts the change via chat real-time and publishes a cross-module event.
    /// </summary>
    public async Task<PresenceDto> SetPresenceAsync(string status, string? statusMessage = null)
    {
        try
        {
            var caller = CreateUserCaller();
            var presence = await _presenceService.SetPresenceAsync(caller.UserId, status, statusMessage);

            await _broadcaster.BroadcastAsync(
                "chat-presence", "PresenceChanged", presence, Context.ConnectionAborted);

            if (_eventBus is not null)
            {
                await _eventBus.PublishAsync(new PresenceChangedEvent
                {
                    EventId = Guid.CreateVersion7(),
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
        try
        {
            var userId = GetUserId();
            await _chatApiClient.SendCallOfferAsync(
                callId, targetUserId, sdpOffer, userId, Context.ConnectionAborted);
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
        try
        {
            var userId = GetUserId();
            await _chatApiClient.SendCallAnswerAsync(
                callId, targetUserId, sdpAnswer, userId, Context.ConnectionAborted);
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
        try
        {
            var userId = GetUserId();
            await _chatApiClient.SendIceCandidateAsync(
                callId, targetUserId, candidate, userId, Context.ConnectionAborted);
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
        try
        {
            var userId = GetUserId();
            await _chatApiClient.SendMediaStateChangeAsync(
                callId, mediaType, enabled, userId, Context.ConnectionAborted);
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
    /// </summary>
    /// <param name="callId">The active call to invite the user to.</param>
    /// <param name="targetUserId">The user to invite.</param>
    public async Task InviteToCallAsync(Guid callId, Guid targetUserId)
    {
        try
        {
            var userId = GetUserId();
            await _chatApiClient.InviteToCallAsync(callId, targetUserId, userId, Context.ConnectionAborted);

            _logger.LogInformation(
                "User {UserId} invited {TargetUserId} to call {CallId}",
                userId, targetUserId, callId);
        }
        catch (Exception ex) when (TryConvertToHubException(ex, out var hubException))
        {
            throw hubException;
        }
    }

    /// <summary>
    /// Transfers the host role of an active call to another participant.
    /// Only the current host may transfer.
    /// </summary>
    /// <param name="callId">The active call.</param>
    /// <param name="newHostUserId">The participant to become the new host.</param>
    public async Task TransferHostAsync(Guid callId, Guid newHostUserId)
    {
        try
        {
            var userId = GetUserId();
            await _chatApiClient.TransferCallHostAsync(callId, newHostUserId, userId, Context.ConnectionAborted);

            _logger.LogInformation(
                "User {UserId} transferred host of call {CallId} to {NewHostUserId}",
                userId, callId, newHostUserId);
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
}
