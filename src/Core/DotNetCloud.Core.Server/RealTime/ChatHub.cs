using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
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
    private readonly IMessageService _messageService;
    private readonly IChannelMemberService _channelMemberService;
    private readonly IReactionService _reactionService;
    private readonly ITypingIndicatorService _typingIndicatorService;
    private readonly IChatRealtimeService _chatRealtimeService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IMessageService messageService,
        IChannelMemberService channelMemberService,
        IReactionService reactionService,
        ITypingIndicatorService typingIndicatorService,
        IChatRealtimeService chatRealtimeService,
        ILogger<ChatHub> logger)
    {
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _channelMemberService = channelMemberService ?? throw new ArgumentNullException(nameof(channelMemberService));
        _reactionService = reactionService ?? throw new ArgumentNullException(nameof(reactionService));
        _typingIndicatorService = typingIndicatorService ?? throw new ArgumentNullException(nameof(typingIndicatorService));
        _chatRealtimeService = chatRealtimeService ?? throw new ArgumentNullException(nameof(chatRealtimeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sends a new message to a channel and broadcasts it to channel members.
    /// </summary>
    public async Task<MessageDto> SendMessageAsync(Guid channelId, string content, Guid? replyToId = null)
    {
        try
        {
            var message = await _messageService.SendMessageAsync(
                channelId,
                new SendMessageDto { Content = content, ReplyToMessageId = replyToId },
                CreateUserCaller(),
                Context.ConnectionAborted);

            await _chatRealtimeService.BroadcastNewMessageAsync(channelId, message, Context.ConnectionAborted);
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
        try
        {
            var message = await _messageService.EditMessageAsync(
                messageId,
                new EditMessageDto { Content = newContent },
                CreateUserCaller(),
                Context.ConnectionAborted);

            await _chatRealtimeService.BroadcastMessageEditedAsync(message.ChannelId, message, Context.ConnectionAborted);
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
            var caller = CreateUserCaller();
            var message = await _messageService.GetMessageAsync(messageId, caller, Context.ConnectionAborted)
                ?? throw new InvalidOperationException($"Message {messageId} not found.");

            await _messageService.DeleteMessageAsync(messageId, caller, Context.ConnectionAborted);
            await _chatRealtimeService.BroadcastMessageDeletedAsync(message.ChannelId, messageId, Context.ConnectionAborted);
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
            var caller = CreateUserCaller();
            await _typingIndicatorService.NotifyTypingAsync(channelId, caller, Context.ConnectionAborted);
            await _chatRealtimeService.BroadcastTypingAsync(channelId, caller.UserId, displayName, Context.ConnectionAborted);
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
        var caller = CreateUserCaller();
        await _chatRealtimeService.BroadcastTypingAsync(channelId, caller.UserId, displayName: null, Context.ConnectionAborted);
    }

    /// <summary>
    /// Marks a channel as read up to a specific message and pushes unread count update to the caller.
    /// </summary>
    public async Task MarkReadAsync(Guid channelId, Guid messageId)
    {
        try
        {
            var caller = CreateUserCaller();
            await _channelMemberService.MarkAsReadAsync(channelId, messageId, caller, Context.ConnectionAborted);

            var unread = await _channelMemberService.GetUnreadCountsAsync(caller, Context.ConnectionAborted);
            var count = unread.FirstOrDefault(x => x.ChannelId == channelId)?.UnreadCount ?? 0;

            await _chatRealtimeService.BroadcastUnreadCountAsync(caller.UserId, channelId, count, Context.ConnectionAborted);
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
            var caller = CreateUserCaller();
            await _reactionService.AddReactionAsync(messageId, emoji, caller, Context.ConnectionAborted);

            var message = await _messageService.GetMessageAsync(messageId, caller, Context.ConnectionAborted)
                ?? throw new InvalidOperationException($"Message {messageId} not found.");
            var reactions = await _reactionService.GetReactionsAsync(messageId, Context.ConnectionAborted);

            await _chatRealtimeService.BroadcastReactionUpdatedAsync(message.ChannelId, messageId, reactions, Context.ConnectionAborted);
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
            var caller = CreateUserCaller();
            await _reactionService.RemoveReactionAsync(messageId, emoji, caller, Context.ConnectionAborted);

            var message = await _messageService.GetMessageAsync(messageId, caller, Context.ConnectionAborted)
                ?? throw new InvalidOperationException($"Message {messageId} not found.");
            var reactions = await _reactionService.GetReactionsAsync(messageId, Context.ConnectionAborted);

            await _chatRealtimeService.BroadcastReactionUpdatedAsync(message.ChannelId, messageId, reactions, Context.ConnectionAborted);
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
