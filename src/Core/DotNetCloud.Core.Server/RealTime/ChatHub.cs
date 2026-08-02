using System.Collections.Concurrent;
using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Chat;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// SignalR hub for chat-specific real-time operations.
/// Handles messaging, reactions, typing indicators, and read receipts.
/// All clients must be authenticated to connect.
/// </summary>
[Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]
internal sealed class ChatHub : Hub
{
    private readonly IChatApiClient _chatApiClient;
    private readonly IRealtimeBroadcaster _broadcaster;
    private readonly ILogger<ChatHub> _logger;

    // Tracks messageId → channelId for reaction broadcasts where the
    // caller only provides the messageId and we need the channel group.
    private static readonly ConcurrentDictionary<Guid, Guid> MessageChannelMap = new();

    public ChatHub(
        IChatApiClient chatApiClient,
        IRealtimeBroadcaster broadcaster,
        ILogger<ChatHub> logger)
    {
        _chatApiClient = chatApiClient ?? throw new ArgumentNullException(nameof(chatApiClient));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static string ChannelGroup(Guid channelId) => $"chat-channel-{channelId}";

    /// <summary>
    /// Sends a new message to a channel and broadcasts it to channel members.
    /// </summary>
    public async Task<ChatMessageDto> SendMessageAsync(Guid channelId, string content, Guid? replyToId = null)
    {
        try
        {
            var userId = GetUserId();
            var message = await _chatApiClient.SendMessageAsync(
                channelId, userId, content, replyToId, Context.ConnectionAborted);

            if (message is null)
                throw new HubException("Failed to send message.");

            // Track messageId → channelId for reaction lookups
            MessageChannelMap[message.Id] = channelId;

            await _broadcaster.BroadcastAsync(
                ChannelGroup(channelId), "NewMessage",
                new { channelId, message }, Context.ConnectionAborted);
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
    public async Task<ChatMessageDto> EditMessageAsync(Guid messageId, string newContent)
    {
        try
        {
            var userId = GetUserId();
            var message = await _chatApiClient.EditMessageAsync(
                messageId, userId, newContent, Context.ConnectionAborted);

            if (message is null)
                throw new HubException($"Message {messageId} not found.");

            // Track messageId → channelId for reaction lookups
            MessageChannelMap[message.Id] = message.ChannelId;

            await _broadcaster.BroadcastAsync(
                ChannelGroup(message.ChannelId), "MessageEdited",
                new { channelId = message.ChannelId, message }, Context.ConnectionAborted);
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
        try
        {
            var userId = GetUserId();

            // Look up channelId from cache; if not found, proceed without broadcast
            MessageChannelMap.TryGetValue(messageId, out var channelId);

            await _chatApiClient.DeleteMessageAsync(messageId, userId, Context.ConnectionAborted);

            if (channelId != Guid.Empty)
            {
                await _broadcaster.BroadcastAsync(
                    ChannelGroup(channelId), "MessageDeleted",
                    new { channelId, messageId }, Context.ConnectionAborted);
            }
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
        try
        {
            var userId = GetUserId();
            await _chatApiClient.NotifyTypingAsync(channelId, userId, Context.ConnectionAborted);

            await _broadcaster.BroadcastAsync(
                ChannelGroup(channelId), "TypingIndicator",
                new { channelId, userId, displayName }, Context.ConnectionAborted);
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
        var userId = GetUserId();
        await _broadcaster.BroadcastAsync(
            ChannelGroup(channelId), "TypingIndicator",
            new { channelId, userId, displayName = (string?)null }, Context.ConnectionAborted);
    }

    /// <summary>
    /// Marks a channel as read up to a specific message.
    /// </summary>
    public async Task MarkReadAsync(Guid channelId, Guid messageId)
    {
        try
        {
            var userId = GetUserId();
            await _chatApiClient.MarkChannelAsReadAsync(channelId, messageId, userId, Context.ConnectionAborted);

            var unread = await _chatApiClient.GetUnreadCountsAsync(userId, Context.ConnectionAborted);
            var channelUnread = unread.FirstOrDefault(x => x.ChannelId == channelId);
            var count = channelUnread?.UnreadCount ?? 0;
            var hasMention = (channelUnread?.MentionCount ?? 0) > 0;

            await _broadcaster.SendToUserAsync(
                userId, "UnreadCountUpdated",
                new { channelId, count, hasMention }, Context.ConnectionAborted);
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
        try
        {
            var userId = GetUserId();
            var added = await _chatApiClient.AddReactionAsync(messageId, userId, emoji, Context.ConnectionAborted);

            if (added && MessageChannelMap.TryGetValue(messageId, out var channelId))
            {
                await _broadcaster.BroadcastAsync(
                    ChannelGroup(channelId), "ReactionUpdated",
                    new { channelId, messageId }, Context.ConnectionAborted);
            }
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
        try
        {
            var userId = GetUserId();
            var removed = await _chatApiClient.RemoveReactionAsync(messageId, userId, emoji, Context.ConnectionAborted);

            if (removed && MessageChannelMap.TryGetValue(messageId, out var channelId))
            {
                await _broadcaster.BroadcastAsync(
                    ChannelGroup(channelId), "ReactionUpdated",
                    new { channelId, messageId }, Context.ConnectionAborted);
            }
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
            hubException = new HubException("The requested operation could not be completed.");
            return true;
        }

        hubException = null!;
        return false;
    }
}
