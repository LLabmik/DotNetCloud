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
/// </summary>
[Authorize]
internal sealed class CoreHub : Hub
{
    private readonly UserConnectionTracker _connectionTracker;
    private readonly PresenceService _presenceService;
    private readonly IMessageService? _messageService;
    private readonly IChannelMemberService? _channelMemberService;
    private readonly IReactionService? _reactionService;
    private readonly ITypingIndicatorService? _typingIndicatorService;
    private readonly IChatRealtimeService? _chatRealtimeService;
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
        IEventBus? eventBus = null)
    {
        _connectionTracker = connectionTracker;
        _presenceService = presenceService;
        _messageService = messageService;
        _channelMemberService = channelMemberService;
        _reactionService = reactionService;
        _typingIndicatorService = typingIndicatorService;
        _chatRealtimeService = chatRealtimeService;
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
    /// Joins the calling user to a broadcast group (e.g., a chat channel or room).
    /// </summary>
    /// <param name="groupName">The group to join.</param>
    public async Task JoinGroupAsync(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new HubException("Group name cannot be empty.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "User {UserId} joined group {Group} via connection {ConnectionId}",
            GetUserId(), groupName, Context.ConnectionId);
    }

    /// <summary>
    /// Removes the calling user from a broadcast group.
    /// </summary>
    /// <param name="groupName">The group to leave.</param>
    public async Task LeaveGroupAsync(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new HubException("Group name cannot be empty.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "User {UserId} left group {Group} via connection {ConnectionId}",
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

    private Guid GetUserId()
    {
        var nameIdentifier = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

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
        if (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            hubException = new HubException(ex.Message);
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
}
