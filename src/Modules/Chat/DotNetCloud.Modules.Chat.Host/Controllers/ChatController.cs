using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Chat.Host.Controllers;

/// <summary>
/// REST API controller for chat channel and message operations.
/// Provides CRUD, messaging, reactions, pins, member management, and search.
/// </summary>
[ApiController]
[Route("api/v1/chat")]
public class ChatController : ControllerBase
{
    private readonly IChannelService _channelService;
    private readonly IChannelMemberService _memberService;
    private readonly IMessageService _messageService;
    private readonly IReactionService _reactionService;
    private readonly IPinService _pinService;
    private readonly ITypingIndicatorService _typingService;
    private readonly ILogger<ChatController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatController"/> class.
    /// </summary>
    public ChatController(
        IChannelService channelService,
        IChannelMemberService memberService,
        IMessageService messageService,
        IReactionService reactionService,
        IPinService pinService,
        ITypingIndicatorService typingService,
        ILogger<ChatController> logger)
    {
        _channelService = channelService;
        _memberService = memberService;
        _messageService = messageService;
        _reactionService = reactionService;
        _pinService = pinService;
        _typingService = typingService;
        _logger = logger;
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static CallerContext ToCaller(Guid userId)
        => new(userId, ["user"], CallerType.User);

    private static object Envelope(object data) => new { success = true, data };
    private static object ErrorEnvelope(string code, string message)
        => new { success = false, error = new { code, message } };

    // ── Channel Endpoints ───────────────────────────────────────────

    /// <summary>Creates a new channel.</summary>
    [HttpPost("channels")]
    public async Task<IActionResult> CreateChannelAsync([FromBody] CreateChannelDto dto, [FromQuery] Guid userId)
    {
        try
        {
            var channel = await _channelService.CreateChannelAsync(dto, ToCaller(userId));
            return CreatedAtAction(nameof(GetChannelAsync), new { channelId = channel.Id }, Envelope(channel));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>Lists channels the caller belongs to.</summary>
    [HttpGet("channels")]
    public async Task<IActionResult> ListChannelsAsync([FromQuery] Guid userId)
    {
        var channels = await _channelService.ListChannelsAsync(ToCaller(userId));
        return Ok(Envelope(channels));
    }

    /// <summary>Gets a channel by ID.</summary>
    [HttpGet("channels/{channelId:guid}")]
    public async Task<IActionResult> GetChannelAsync(Guid channelId, [FromQuery] Guid userId)
    {
        var channel = await _channelService.GetChannelAsync(channelId, ToCaller(userId));
        if (channel is null)
            return NotFound(ErrorEnvelope("CHAT_CHANNEL_NOT_FOUND", "Channel not found."));

        return Ok(Envelope(channel));
    }

    /// <summary>Updates a channel.</summary>
    [HttpPut("channels/{channelId:guid}")]
    public async Task<IActionResult> UpdateChannelAsync(Guid channelId, [FromBody] UpdateChannelDto dto, [FromQuery] Guid userId)
    {
        try
        {
            var channel = await _channelService.UpdateChannelAsync(channelId, dto, ToCaller(userId));
            return Ok(Envelope(channel));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ErrorEnvelope("CHAT_CHANNEL_NOT_FOUND", "Channel not found."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Deletes a channel (soft-delete).</summary>
    [HttpDelete("channels/{channelId:guid}")]
    public async Task<IActionResult> DeleteChannelAsync(Guid channelId, [FromQuery] Guid userId)
    {
        try
        {
            await _channelService.DeleteChannelAsync(channelId, ToCaller(userId));
            return Ok(Envelope(new { deleted = true }));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ErrorEnvelope("CHAT_CHANNEL_NOT_FOUND", "Channel not found."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Archives a channel.</summary>
    [HttpPost("channels/{channelId:guid}/archive")]
    public async Task<IActionResult> ArchiveChannelAsync(Guid channelId, [FromQuery] Guid userId)
    {
        try
        {
            await _channelService.ArchiveChannelAsync(channelId, ToCaller(userId));
            return Ok(Envelope(new { archived = true }));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ErrorEnvelope("CHAT_CHANNEL_NOT_FOUND", "Channel not found."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Gets or creates a DM channel with another user.</summary>
    [HttpPost("channels/dm/{otherUserId:guid}")]
    public async Task<IActionResult> GetOrCreateDmAsync(Guid otherUserId, [FromQuery] Guid userId)
    {
        var channel = await _channelService.GetOrCreateDirectMessageAsync(otherUserId, ToCaller(userId));
        return Ok(Envelope(channel));
    }

    // ── Member Endpoints ────────────────────────────────────────────

    /// <summary>Adds a member to a channel.</summary>
    [HttpPost("channels/{channelId:guid}/members")]
    public async Task<IActionResult> AddMemberAsync(Guid channelId, [FromBody] AddChannelMemberDto dto, [FromQuery] Guid userId)
    {
        await _memberService.AddMemberAsync(channelId, dto.UserId, ToCaller(userId));
        return Ok(Envelope(new { added = true }));
    }

    /// <summary>Removes a member from a channel.</summary>
    [HttpDelete("channels/{channelId:guid}/members/{targetUserId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(Guid channelId, Guid targetUserId, [FromQuery] Guid userId)
    {
        await _memberService.RemoveMemberAsync(channelId, targetUserId, ToCaller(userId));
        return Ok(Envelope(new { removed = true }));
    }

    /// <summary>Lists members of a channel.</summary>
    [HttpGet("channels/{channelId:guid}/members")]
    public async Task<IActionResult> GetMembersAsync(Guid channelId, [FromQuery] Guid userId)
    {
        var members = await _memberService.ListMembersAsync(channelId, ToCaller(userId));
        return Ok(Envelope(members));
    }

    /// <summary>Updates a member's role in a channel.</summary>
    [HttpPut("channels/{channelId:guid}/members/{targetUserId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRoleAsync(
        Guid channelId, Guid targetUserId, [FromBody] UpdateMemberRoleDto dto, [FromQuery] Guid userId)
    {
        if (!Enum.TryParse<ChannelMemberRole>(dto.Role, ignoreCase: true, out var role))
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", "Invalid role."));

        try
        {
            await _memberService.UpdateMemberRoleAsync(channelId, targetUserId, role, ToCaller(userId));
            return Ok(Envelope(new { updated = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_MEMBER_NOT_FOUND", ex.Message));
        }
    }

    /// <summary>Updates the caller's notification preference for a channel.</summary>
    [HttpPut("channels/{channelId:guid}/notifications")]
    public async Task<IActionResult> UpdateNotificationPreferenceAsync(
        Guid channelId, [FromBody] UpdateNotificationPrefDto dto, [FromQuery] Guid userId)
    {
        if (!Enum.TryParse<NotificationPreference>(dto.Preference, ignoreCase: true, out var pref))
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", "Invalid notification preference."));

        try
        {
            await _memberService.UpdateNotificationPreferenceAsync(channelId, pref, ToCaller(userId));
            return Ok(Envelope(new { updated = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_MEMBER_NOT_FOUND", ex.Message));
        }
    }

    /// <summary>Marks a channel as read up to a message.</summary>
    [HttpPost("channels/{channelId:guid}/read")]
    public async Task<IActionResult> MarkAsReadAsync(Guid channelId, [FromBody] MarkReadDto dto, [FromQuery] Guid userId)
    {
        try
        {
            await _memberService.MarkAsReadAsync(channelId, dto.MessageId, ToCaller(userId));
            return Ok(Envelope(new { marked = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_MEMBER_NOT_FOUND", ex.Message));
        }
    }

    /// <summary>Gets unread counts for all channels the caller belongs to.</summary>
    [HttpGet("unread")]
    public async Task<IActionResult> GetUnreadCountsAsync([FromQuery] Guid userId)
    {
        var unread = await _memberService.GetUnreadCountsAsync(ToCaller(userId));
        return Ok(Envelope(unread));
    }

    // ── Message Endpoints ───────────────────────────────────────────

    /// <summary>Sends a message to a channel.</summary>
    [HttpPost("channels/{channelId:guid}/messages")]
    public async Task<IActionResult> SendMessageAsync(Guid channelId, [FromBody] SendMessageDto dto, [FromQuery] Guid userId)
    {
        try
        {
            var message = await _messageService.SendMessageAsync(channelId, dto, ToCaller(userId));
            return Ok(Envelope(message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Gets paginated messages from a channel.</summary>
    [HttpGet("channels/{channelId:guid}/messages")]
    public async Task<IActionResult> GetMessagesAsync(
        Guid channelId,
        [FromQuery] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _messageService.GetMessagesAsync(channelId, page, pageSize, ToCaller(userId));
        return Ok(new
        {
            success = true,
            data = result.Items,
            pagination = new
            {
                page = result.Page,
                pageSize = result.PageSize,
                totalItems = result.TotalItems,
                totalPages = result.TotalPages
            }
        });
    }

    /// <summary>Gets a single message by ID.</summary>
    [HttpGet("channels/{channelId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> GetMessageAsync(Guid channelId, Guid messageId, [FromQuery] Guid userId)
    {
        var message = await _messageService.GetMessageAsync(messageId, ToCaller(userId));
        if (message is null)
            return NotFound(ErrorEnvelope("CHAT_MESSAGE_NOT_FOUND", "Message not found."));

        return Ok(Envelope(message));
    }

    /// <summary>Edits a message.</summary>
    [HttpPut("channels/{channelId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> EditMessageAsync(Guid channelId, Guid messageId, [FromBody] EditMessageDto dto, [FromQuery] Guid userId)
    {
        try
        {
            var message = await _messageService.EditMessageAsync(messageId, dto, ToCaller(userId));
            return Ok(Envelope(message));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ErrorEnvelope("CHAT_MESSAGE_NOT_FOUND", "Message not found."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Deletes a message (soft-delete).</summary>
    [HttpDelete("channels/{channelId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessageAsync(Guid channelId, Guid messageId, [FromQuery] Guid userId)
    {
        try
        {
            await _messageService.DeleteMessageAsync(messageId, ToCaller(userId));
            return Ok(Envelope(new { deleted = true }));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ErrorEnvelope("CHAT_MESSAGE_NOT_FOUND", "Message not found."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Searches messages in a channel.</summary>
    [HttpGet("channels/{channelId:guid}/messages/search")]
    public async Task<IActionResult> SearchMessagesAsync(
        Guid channelId,
        [FromQuery] string q,
        [FromQuery] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", "Search query is required."));

        var result = await _messageService.SearchMessagesAsync(channelId, q, page, pageSize, ToCaller(userId));
        return Ok(new
        {
            success = true,
            data = result.Items,
            pagination = new
            {
                page = result.Page,
                pageSize = result.PageSize,
                totalItems = result.TotalItems,
                totalPages = result.TotalPages
            }
        });
    }

    // ── Reaction Endpoints ──────────────────────────────────────────

    /// <summary>Adds a reaction to a message.</summary>
    [HttpPost("messages/{messageId:guid}/reactions")]
    public async Task<IActionResult> AddReactionAsync(Guid messageId, [FromBody] AddReactionDto dto, [FromQuery] Guid userId)
    {
        try
        {
            await _reactionService.AddReactionAsync(messageId, dto.Emoji, ToCaller(userId));
            return Ok(Envelope(new { added = true }));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ErrorEnvelope("CHAT_MESSAGE_NOT_FOUND", "Message not found."));
        }
    }

    /// <summary>Removes a reaction from a message.</summary>
    [HttpDelete("messages/{messageId:guid}/reactions/{emoji}")]
    public async Task<IActionResult> RemoveReactionAsync(Guid messageId, string emoji, [FromQuery] Guid userId)
    {
        await _reactionService.RemoveReactionAsync(messageId, emoji, ToCaller(userId));
        return Ok(Envelope(new { removed = true }));
    }

    /// <summary>Gets all reactions for a message.</summary>
    [HttpGet("messages/{messageId:guid}/reactions")]
    public async Task<IActionResult> GetReactionsAsync(Guid messageId)
    {
        var reactions = await _reactionService.GetReactionsAsync(messageId);
        return Ok(Envelope(reactions));
    }

    // ── Pin Endpoints ───────────────────────────────────────────────

    /// <summary>Pins a message in a channel.</summary>
    [HttpPost("channels/{channelId:guid}/pins/{messageId:guid}")]
    public async Task<IActionResult> PinMessageAsync(Guid channelId, Guid messageId, [FromQuery] Guid userId)
    {
        await _pinService.PinMessageAsync(channelId, messageId, ToCaller(userId));
        return Ok(Envelope(new { pinned = true }));
    }

    /// <summary>Unpins a message from a channel.</summary>
    [HttpDelete("channels/{channelId:guid}/pins/{messageId:guid}")]
    public async Task<IActionResult> UnpinMessageAsync(Guid channelId, Guid messageId, [FromQuery] Guid userId)
    {
        await _pinService.UnpinMessageAsync(channelId, messageId, ToCaller(userId));
        return Ok(Envelope(new { unpinned = true }));
    }

    /// <summary>Gets pinned messages in a channel.</summary>
    [HttpGet("channels/{channelId:guid}/pins")]
    public async Task<IActionResult> GetPinnedMessagesAsync(Guid channelId, [FromQuery] Guid userId)
    {
        var pins = await _pinService.GetPinnedMessagesAsync(channelId, ToCaller(userId));
        return Ok(Envelope(pins));
    }

    // ── Typing Indicator Endpoints ──────────────────────────────────

    /// <summary>Notifies that the caller is typing in a channel.</summary>
    [HttpPost("channels/{channelId:guid}/typing")]
    public async Task<IActionResult> NotifyTypingAsync(Guid channelId, [FromQuery] Guid userId)
    {
        await _typingService.NotifyTypingAsync(channelId, ToCaller(userId));
        return Ok(Envelope(new { typing = true }));
    }

    /// <summary>Gets users currently typing in a channel.</summary>
    [HttpGet("channels/{channelId:guid}/typing")]
    public async Task<IActionResult> GetTypingUsersAsync(Guid channelId)
    {
        var users = await _typingService.GetTypingUsersAsync(channelId);
        return Ok(Envelope(users));
    }

    // ── File Sharing Endpoints ──────────────────────────────────────

    /// <summary>Lists files shared in a channel (via message attachments).</summary>
    [HttpGet("channels/{channelId:guid}/files")]
    public async Task<IActionResult> GetChannelFilesAsync(Guid channelId, [FromQuery] Guid userId)
    {
        // Retrieve messages with attachments for this channel
        var result = await _messageService.GetMessagesAsync(channelId, 1, 100, ToCaller(userId));
        var attachments = result.Items
            .SelectMany(m => m.Attachments)
            .ToList();
        return Ok(Envelope(attachments));
    }
}

// ── Request DTOs used only by controller endpoints ──────────────────

/// <summary>DTO for updating a member's role.</summary>
public sealed record UpdateMemberRoleDto
{
    /// <summary>The new role: "Owner", "Admin", or "Member".</summary>
    public required string Role { get; init; }
}

/// <summary>DTO for updating notification preference.</summary>
public sealed record UpdateNotificationPrefDto
{
    /// <summary>The preference: "All", "Mentions", or "None".</summary>
    public required string Preference { get; init; }
}

/// <summary>DTO for marking a channel as read.</summary>
public sealed record MarkReadDto
{
    /// <summary>ID of the last-read message.</summary>
    public Guid MessageId { get; init; }
}

/// <summary>DTO for adding a reaction.</summary>
public sealed record AddReactionDto
{
    /// <summary>Emoji character or code.</summary>
    public required string Emoji { get; init; }
}
