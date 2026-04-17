using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using DotNetCloud.Modules.Search.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using ValidationException = DotNetCloud.Core.Errors.ValidationException;

namespace DotNetCloud.Modules.Chat.Host.Controllers;

/// <summary>
/// REST API controller for chat channel and message operations.
/// Provides CRUD, messaging, reactions, pins, member management, and search.
/// </summary>
[Route("api/v1/chat")]
public class ChatController : ChatControllerBase
{
    private readonly IChannelService _channelService;
    private readonly IChannelMemberService _memberService;
    private readonly IMessageService _messageService;
    private readonly IReactionService _reactionService;
    private readonly IPinService _pinService;
    private readonly ITypingIndicatorService _typingService;
    private readonly IAnnouncementService _announcementService;
    private readonly IChannelInviteService _inviteService;
    private readonly IRealtimeBroadcaster _realtimeBroadcaster;
    private readonly IChatRealtimeService _chatRealtimeService;
    private readonly IChatMessageNotifier _chatMessageNotifier;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly INotificationPreferenceStore _notificationPreferenceStore;
    private readonly IIceServerService _iceServerService;
    private readonly IVideoCallService _videoCallService;
    private readonly ILogger<ChatController> _logger;
    private readonly ISearchFtsClient? _searchFtsClient;

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
        IAnnouncementService announcementService,
        IChannelInviteService inviteService,
        IRealtimeBroadcaster realtimeBroadcaster,
        IChatRealtimeService chatRealtimeService,
        IChatMessageNotifier chatMessageNotifier,
        IPushNotificationService pushNotificationService,
        INotificationPreferenceStore notificationPreferenceStore,
        IIceServerService iceServerService,
        IVideoCallService videoCallService,
        ILogger<ChatController> logger,
        ISearchFtsClient? searchFtsClient = null)
    {
        _channelService = channelService;
        _memberService = memberService;
        _messageService = messageService;
        _reactionService = reactionService;
        _pinService = pinService;
        _typingService = typingService;
        _announcementService = announcementService;
        _inviteService = inviteService;
        _realtimeBroadcaster = realtimeBroadcaster;
        _chatRealtimeService = chatRealtimeService;
        _chatMessageNotifier = chatMessageNotifier;
        _pushNotificationService = pushNotificationService;
        _notificationPreferenceStore = notificationPreferenceStore;
        _iceServerService = iceServerService;
        _videoCallService = videoCallService;
        _logger = logger;
        _searchFtsClient = searchFtsClient;
    }

    // ── Channel Endpoints ───────────────────────────────────────────

    /// <summary>Creates a new channel.</summary>
    [HttpPost("channels")]
    public async Task<IActionResult> CreateChannelAsync([FromBody] CreateChannelDto dto)
    {
        try
        {
            var channel = await _channelService.CreateChannelAsync(dto, GetAuthenticatedCaller());
            return CreatedAtAction("GetChannel", new { channelId = channel.Id }, Envelope(channel));
        }
        catch (ValidationException ex)
        {
            return Conflict(ErrorEnvelope("DUPLICATE_CHANNEL_NAME", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>Lists channels the caller belongs to.</summary>
    [HttpGet("channels")]
    public async Task<IActionResult> ListChannelsAsync()
    {
        var channels = await _channelService.ListChannelsAsync(GetAuthenticatedCaller());
        return Ok(Envelope(channels));
    }

    /// <summary>Gets a channel by ID.</summary>
    [HttpGet("channels/{channelId:guid}")]
    public async Task<IActionResult> GetChannelAsync(Guid channelId)
    {
        var channel = await _channelService.GetChannelAsync(channelId, GetAuthenticatedCaller());
        if (channel is null)
            return NotFound(ErrorEnvelope("CHAT_CHANNEL_NOT_FOUND", "Channel not found."));

        return Ok(Envelope(channel));
    }

    /// <summary>Updates a channel.</summary>
    [HttpPut("channels/{channelId:guid}")]
    public async Task<IActionResult> UpdateChannelAsync(Guid channelId, [FromBody] UpdateChannelDto dto)
    {
        try
        {
            var channel = await _channelService.UpdateChannelAsync(channelId, dto, GetAuthenticatedCaller());
            return Ok(Envelope(channel));
        }
        catch (ValidationException ex)
        {
            return Conflict(ErrorEnvelope("DUPLICATE_CHANNEL_NAME", ex.Message));
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
    public async Task<IActionResult> DeleteChannelAsync(Guid channelId)
    {
        try
        {
            await _channelService.DeleteChannelAsync(channelId, GetAuthenticatedCaller());
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
    public async Task<IActionResult> ArchiveChannelAsync(Guid channelId)
    {
        try
        {
            await _channelService.ArchiveChannelAsync(channelId, GetAuthenticatedCaller());
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
    public async Task<IActionResult> GetOrCreateDmAsync(Guid otherUserId)
    {
        var channel = await _channelService.GetOrCreateDirectMessageAsync(otherUserId, GetAuthenticatedCaller());
        return Ok(Envelope(channel));
    }

    // ── Member Endpoints ────────────────────────────────────────────

    /// <summary>Adds a member to a channel.</summary>
    [HttpPost("channels/{channelId:guid}/members")]
    public async Task<IActionResult> AddMemberAsync(Guid channelId, [FromBody] AddChannelMemberDto dto)
    {
        try
        {
            await _memberService.AddMemberAsync(channelId, dto.UserId, GetAuthenticatedCaller());
            return Ok(Envelope(new { added = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_CHANNEL_OR_MEMBER_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Removes a member from a channel.</summary>
    [HttpDelete("channels/{channelId:guid}/members/{targetUserId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(Guid channelId, Guid targetUserId)
    {
        try
        {
            await _memberService.RemoveMemberAsync(channelId, targetUserId, GetAuthenticatedCaller());
            return Ok(Envelope(new { removed = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_CHANNEL_OR_MEMBER_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Lists members of a channel.</summary>
    [HttpGet("channels/{channelId:guid}/members")]
    public async Task<IActionResult> GetMembersAsync(Guid channelId)
    {
        try
        {
            var members = await _memberService.ListMembersAsync(channelId, GetAuthenticatedCaller());
            return Ok(Envelope(members));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_CHANNEL_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Updates a member's role in a channel.</summary>
    [HttpPut("channels/{channelId:guid}/members/{targetUserId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRoleAsync(
        Guid channelId, Guid targetUserId, [FromBody] UpdateMemberRoleDto dto)
    {
        if (!Enum.TryParse<ChannelMemberRole>(dto.Role, ignoreCase: true, out var role))
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", "Invalid role."));

        try
        {
            await _memberService.UpdateMemberRoleAsync(channelId, targetUserId, role, GetAuthenticatedCaller());
            return Ok(Envelope(new { updated = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_MEMBER_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Updates the caller's notification preference for a channel.</summary>
    [HttpPut("channels/{channelId:guid}/notifications")]
    public async Task<IActionResult> UpdateNotificationPreferenceAsync(
        Guid channelId, [FromBody] UpdateNotificationPrefDto dto)
    {
        if (!Enum.TryParse<NotificationPreference>(dto.Preference, ignoreCase: true, out var pref))
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", "Invalid notification preference."));

        try
        {
            await _memberService.UpdateNotificationPreferenceAsync(channelId, pref, GetAuthenticatedCaller());
            return Ok(Envelope(new { updated = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_MEMBER_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Marks a channel as read up to a message.</summary>
    [HttpPost("channels/{channelId:guid}/read")]
    public async Task<IActionResult> MarkAsReadAsync(Guid channelId, [FromBody] MarkReadDto dto)
    {
        try
        {
            await _memberService.MarkAsReadAsync(channelId, dto.MessageId, GetAuthenticatedCaller());
            return Ok(Envelope(new { marked = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_MEMBER_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Gets unread counts for all channels the caller belongs to.</summary>
    [HttpGet("unread")]
    public async Task<IActionResult> GetUnreadCountsAsync()
    {
        var unread = await _memberService.GetUnreadCountsAsync(GetAuthenticatedCaller());
        return Ok(Envelope(unread));
    }

    // ── Message Endpoints ───────────────────────────────────────────

    /// <summary>Sends a message to a channel.</summary>
    [HttpPost("channels/{channelId:guid}/messages")]
    public async Task<IActionResult> SendMessageAsync(Guid channelId, [FromBody] SendMessageDto dto)
    {
        try
        {
            var message = await _messageService.SendMessageAsync(channelId, dto, GetAuthenticatedCaller());
            await _chatRealtimeService.BroadcastNewMessageAsync(channelId, message);
            _chatMessageNotifier.NotifyMessageReceived(channelId, message);
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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _messageService.GetMessagesAsync(channelId, page, pageSize, GetAuthenticatedCaller());
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
    public async Task<IActionResult> GetMessageAsync(Guid channelId, Guid messageId)
    {
        var message = await _messageService.GetMessageAsync(messageId, GetAuthenticatedCaller());
        if (message is null)
            return NotFound(ErrorEnvelope("CHAT_MESSAGE_NOT_FOUND", "Message not found."));

        return Ok(Envelope(message));
    }

    /// <summary>Edits a message.</summary>
    [HttpPut("channels/{channelId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> EditMessageAsync(Guid channelId, Guid messageId, [FromBody] EditMessageDto dto)
    {
        try
        {
            var message = await _messageService.EditMessageAsync(messageId, dto, GetAuthenticatedCaller());
            await _chatRealtimeService.BroadcastMessageEditedAsync(channelId, message);
            _chatMessageNotifier.NotifyMessageEdited(channelId, message);
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
    public async Task<IActionResult> DeleteMessageAsync(Guid channelId, Guid messageId)
    {
        try
        {
            await _messageService.DeleteMessageAsync(messageId, GetAuthenticatedCaller());
            await _chatRealtimeService.BroadcastMessageDeletedAsync(channelId, messageId);
            _chatMessageNotifier.NotifyMessageDeleted(channelId, messageId);
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

    /// <summary>Searches messages in a channel using full-text search when available.</summary>
    [HttpGet("channels/{channelId:guid}/messages/search")]
    public async Task<IActionResult> SearchMessagesAsync(
        Guid channelId,
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", "Search query is required."));

        var caller = GetAuthenticatedCaller();

        // Try FTS via Search module gRPC when available
        if (_searchFtsClient is { IsAvailable: true })
        {
            var ftsResult = await _searchFtsClient.SearchAsync(
                q, moduleFilter: "chat", entityTypeFilter: "Message",
                userId: caller.UserId, page: page, pageSize: pageSize);

            if (ftsResult is not null)
            {
                return Ok(new
                {
                    success = true,
                    data = ftsResult.Items,
                    pagination = new
                    {
                        page = ftsResult.Page,
                        pageSize = ftsResult.PageSize,
                        totalItems = ftsResult.TotalCount,
                        totalPages = ftsResult.TotalCount > 0
                            ? (int)Math.Ceiling((double)ftsResult.TotalCount / ftsResult.PageSize)
                            : 0
                    }
                });
            }
        }

        // Fallback to LIKE-based search
        var result = await _messageService.SearchMessagesAsync(channelId, q, page, pageSize, caller);
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
    public async Task<IActionResult> AddReactionAsync(Guid messageId, [FromBody] AddReactionDto dto)
    {
        try
        {
            await _reactionService.AddReactionAsync(messageId, dto.Emoji, GetAuthenticatedCaller());
            return Ok(Envelope(new { added = true }));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ErrorEnvelope("CHAT_MESSAGE_NOT_FOUND", "Message not found."));
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

    /// <summary>Removes a reaction from a message.</summary>
    [HttpDelete("messages/{messageId:guid}/reactions/{emoji}")]
    public async Task<IActionResult> RemoveReactionAsync(Guid messageId, string emoji)
    {
        try
        {
            await _reactionService.RemoveReactionAsync(messageId, emoji, GetAuthenticatedCaller());
            return Ok(Envelope(new { removed = true }));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ErrorEnvelope("CHAT_MESSAGE_NOT_FOUND", "Message not found."));
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
    public async Task<IActionResult> PinMessageAsync(Guid channelId, Guid messageId)
    {
        try
        {
            await _pinService.PinMessageAsync(channelId, messageId, GetAuthenticatedCaller());
            return Ok(Envelope(new { pinned = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_PIN_TARGET_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Unpins a message from a channel.</summary>
    [HttpDelete("channels/{channelId:guid}/pins/{messageId:guid}")]
    public async Task<IActionResult> UnpinMessageAsync(Guid channelId, Guid messageId)
    {
        try
        {
            await _pinService.UnpinMessageAsync(channelId, messageId, GetAuthenticatedCaller());
            return Ok(Envelope(new { unpinned = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_PIN_TARGET_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Gets pinned messages in a channel.</summary>
    [HttpGet("channels/{channelId:guid}/pins")]
    public async Task<IActionResult> GetPinnedMessagesAsync(Guid channelId)
    {
        try
        {
            var pins = await _pinService.GetPinnedMessagesAsync(channelId, GetAuthenticatedCaller());
            return Ok(Envelope(pins));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CHAT_CHANNEL_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // ── Typing Indicator Endpoints ──────────────────────────────────

    /// <summary>Notifies that the caller is typing in a channel.</summary>
    [HttpPost("channels/{channelId:guid}/typing")]
    public async Task<IActionResult> NotifyTypingAsync(Guid channelId)
    {
        try
        {
            await _typingService.NotifyTypingAsync(channelId, GetAuthenticatedCaller());
            return Ok(Envelope(new { typing = true }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>Gets users currently typing in a channel.</summary>
    [HttpGet("channels/{channelId:guid}/typing")]
    public async Task<IActionResult> GetTypingUsersAsync(Guid channelId)
    {
        try
        {
            var users = await _typingService.GetTypingUsersAsync(channelId);
            return Ok(Envelope(users));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
        }
    }

    // ── File Sharing Endpoints ──────────────────────────────────────

    /// <summary>Attaches a file to an existing message.</summary>
    [HttpPost("channels/{channelId:guid}/messages/{messageId:guid}/attachments")]
    public async Task<IActionResult> AddAttachmentAsync(
        Guid channelId, Guid messageId, [FromBody] CreateAttachmentDto dto)
    {
        try
        {
            var attachment = await _messageService.AddAttachmentAsync(channelId, messageId, dto, GetAuthenticatedCaller());
            return Ok(Envelope(attachment));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
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

    /// <summary>Lists files shared in a channel (via message attachments).</summary>
    [HttpGet("channels/{channelId:guid}/files")]
    public async Task<IActionResult> GetChannelFilesAsync(Guid channelId)
    {
        // Retrieve messages with attachments for this channel
        var result = await _messageService.GetMessagesAsync(channelId, 1, 100, GetAuthenticatedCaller());
        var attachments = result.Items
            .SelectMany(m => m.Attachments)
            .ToList();
        return Ok(Envelope(attachments));
    }

    // ── Announcement Endpoints ─────────────────────────────────────

    /// <summary>Creates an organization-wide announcement.</summary>
    [HttpPost("~/api/v1/announcements")]
    public async Task<IActionResult> CreateAnnouncementAsync([FromBody] CreateAnnouncementDto dto)
    {
        try
        {
            var announcement = await _announcementService.CreateAsync(dto, GetAuthenticatedCaller());
            await _realtimeBroadcaster.BroadcastAsync("announcements", "AnnouncementCreated", announcement);

            if (string.Equals(announcement.Priority, "Urgent", StringComparison.OrdinalIgnoreCase))
            {
                await _realtimeBroadcaster.BroadcastAsync("announcements", "UrgentAnnouncement", announcement);
            }

            var announcementCount = (await _announcementService.ListAsync(GetAuthenticatedCaller())).Count;
            await _realtimeBroadcaster.BroadcastAsync(
                "announcements",
                "AnnouncementBadgeUpdated",
                new { count = announcementCount });

            return CreatedAtAction("GetAnnouncement", new { id = announcement.Id }, Envelope(announcement));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>Lists active announcements for the caller.</summary>
    [HttpGet("~/api/v1/announcements")]
    public async Task<IActionResult> ListAnnouncementsAsync()
    {
        var announcements = await _announcementService.ListAsync(GetAuthenticatedCaller());
        return Ok(Envelope(announcements));
    }

    /// <summary>Gets a single announcement by ID.</summary>
    [HttpGet("~/api/v1/announcements/{id:guid}")]
    public async Task<IActionResult> GetAnnouncementAsync(Guid id)
    {
        var announcement = await _announcementService.GetAsync(id, GetAuthenticatedCaller());
        if (announcement is null)
            return NotFound(ErrorEnvelope("ANNOUNCEMENT_NOT_FOUND", "Announcement not found."));

        return Ok(Envelope(announcement));
    }

    /// <summary>Updates an announcement.</summary>
    [HttpPut("~/api/v1/announcements/{id:guid}")]
    public async Task<IActionResult> UpdateAnnouncementAsync(Guid id, [FromBody] UpdateAnnouncementDto dto)
    {
        try
        {
            await _announcementService.UpdateAsync(id, dto, GetAuthenticatedCaller());
            return Ok(Envelope(new { updated = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("ANNOUNCEMENT_NOT_FOUND", ex.Message));
        }
    }

    /// <summary>Deletes an announcement (soft-delete).</summary>
    [HttpDelete("~/api/v1/announcements/{id:guid}")]
    public async Task<IActionResult> DeleteAnnouncementAsync(Guid id)
    {
        try
        {
            await _announcementService.DeleteAsync(id, GetAuthenticatedCaller());
            return Ok(Envelope(new { deleted = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("ANNOUNCEMENT_NOT_FOUND", ex.Message));
        }
    }

    /// <summary>Acknowledges an announcement for the caller.</summary>
    [HttpPost("~/api/v1/announcements/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAnnouncementAsync(Guid id)
    {
        await _announcementService.AcknowledgeAsync(id, GetAuthenticatedCaller());
        return Ok(Envelope(new { acknowledged = true }));
    }

    /// <summary>Gets announcement acknowledgements.</summary>
    [HttpGet("~/api/v1/announcements/{id:guid}/acknowledgements")]
    public async Task<IActionResult> GetAnnouncementAcknowledgementsAsync(Guid id)
    {
        var acknowledgements = await _announcementService.GetAcknowledgementsAsync(id, GetAuthenticatedCaller());
        return Ok(Envelope(acknowledgements));
    }

    // ── Push Notification Endpoints ────────────────────────────────

    // ── Channel Invite Endpoints ──────────────────────────────────

    /// <summary>Sends an invitation for a single user to join a private channel.</summary>
    [HttpPost("channels/{channelId:guid}/invites")]
    public async Task<IActionResult> CreateInviteAsync(Guid channelId, [FromBody] CreateChannelInviteDto dto)
    {
        try
        {
            var invite = await _inviteService.CreateInviteAsync(channelId, dto, GetAuthenticatedCaller());
            return Ok(Envelope(invite));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope("INVITE_ERROR", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Lists pending invitations for the calling user.</summary>
    [HttpGet("invites")]
    public async Task<IActionResult> ListMyInvitesAsync()
    {
        var invites = await _inviteService.ListMyInvitesAsync(GetAuthenticatedCaller());
        return Ok(Envelope(invites));
    }

    /// <summary>Lists pending invitations for a channel.</summary>
    [HttpGet("channels/{channelId:guid}/invites")]
    public async Task<IActionResult> ListChannelInvitesAsync(Guid channelId)
    {
        try
        {
            var invites = await _inviteService.ListChannelInvitesAsync(channelId, GetAuthenticatedCaller());
            return Ok(Envelope(invites));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Accepts a pending channel invitation.</summary>
    [HttpPost("invites/{inviteId:guid}/accept")]
    public async Task<IActionResult> AcceptInviteAsync(Guid inviteId)
    {
        try
        {
            var invite = await _inviteService.AcceptInviteAsync(inviteId, GetAuthenticatedCaller());
            return Ok(Envelope(invite));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope("INVITE_ERROR", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Declines a pending channel invitation.</summary>
    [HttpPost("invites/{inviteId:guid}/decline")]
    public async Task<IActionResult> DeclineInviteAsync(Guid inviteId)
    {
        try
        {
            var invite = await _inviteService.DeclineInviteAsync(inviteId, GetAuthenticatedCaller());
            return Ok(Envelope(invite));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope("INVITE_ERROR", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Revokes a pending channel invitation.</summary>
    [HttpDelete("invites/{inviteId:guid}")]
    public async Task<IActionResult> RevokeInviteAsync(Guid inviteId)
    {
        try
        {
            await _inviteService.RevokeInviteAsync(inviteId, GetAuthenticatedCaller());
            return Ok(Envelope(new { revoked = true }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope("INVITE_ERROR", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Registers the caller device for push notifications.</summary>
    [HttpPost("~/api/v1/notifications/devices/register")]
    public async Task<IActionResult> RegisterPushDeviceAsync([FromBody] RegisterDeviceRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DeviceToken))
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", "Device token is required."));

        if (!Enum.TryParse<PushProvider>(dto.Provider, ignoreCase: true, out var provider))
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", "Invalid push provider."));

        var caller = GetAuthenticatedCaller();
        await _pushNotificationService.RegisterDeviceAsync(caller.UserId, new DeviceRegistration
        {
            Token = dto.DeviceToken,
            Provider = provider,
            Endpoint = dto.Endpoint
        });

        return Ok(Envelope(new { registered = true }));
    }

    /// <summary>Unregisters the caller device from push notifications.</summary>
    [HttpDelete("~/api/v1/notifications/devices/{deviceToken}")]
    public async Task<IActionResult> UnregisterPushDeviceAsync(string deviceToken)
    {
        var caller = GetAuthenticatedCaller();
        await _pushNotificationService.UnregisterDeviceAsync(caller.UserId, deviceToken);
        return Ok(Envelope(new { unregistered = true }));
    }

    /// <summary>Gets caller-level push notification preferences.</summary>
    [HttpGet("~/api/v1/notifications/preferences")]
    public IActionResult GetNotificationPreferencesAsync()
    {
        var caller = GetAuthenticatedCaller();
        var preferences = _notificationPreferenceStore.Get(caller.UserId);
        var dto = new NotificationPreferencesDto
        {
            PushEnabled = preferences.PushEnabled,
            DoNotDisturb = preferences.DoNotDisturb,
            MutedChannelIds = [.. preferences.MutedChannelIds]
        };

        return Ok(Envelope(dto));
    }

    /// <summary>Updates caller-level push notification preferences.</summary>
    [HttpPut("~/api/v1/notifications/preferences")]
    public IActionResult UpdateNotificationPreferencesAsync([FromBody] NotificationPreferencesDto dto)
    {
        var caller = GetAuthenticatedCaller();
        _notificationPreferenceStore.Update(caller.UserId, new UserNotificationPreferences
        {
            PushEnabled = dto.PushEnabled,
            DoNotDisturb = dto.DoNotDisturb,
            MutedChannelIds = dto.MutedChannelIds.Distinct().ToHashSet()
        });

        return Ok(Envelope(new { updated = true }));
    }

    // ── Video Call Endpoints ──────────────────────────────────────

    /// <summary>Initiates a new video call in a channel.</summary>
    [HttpPost("channels/{channelId:guid}/calls")]
    [EnableRateLimiting("module-video-call-initiate")]
    public async Task<IActionResult> InitiateCallAsync(Guid channelId, [FromBody] StartCallRequest request)
    {
        try
        {
            var call = await _videoCallService.InitiateCallAsync(channelId, request, GetAuthenticatedCaller());
            return CreatedAtAction("GetCall", new { callId = call.Id }, Envelope(call));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ErrorEnvelope("CALL_ALREADY_ACTIVE", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Initiates a direct call to a specific user by creating/reusing a DM channel.</summary>
    [HttpPost("calls/direct/{targetUserId:guid}")]
    [EnableRateLimiting("module-video-call-initiate")]
    public async Task<IActionResult> InitiateDirectCallAsync(Guid targetUserId, [FromBody] StartCallRequest request)
    {
        try
        {
            var call = await _videoCallService.InitiateDirectCallAsync(targetUserId, request, GetAuthenticatedCaller());
            return CreatedAtAction("GetCall", new { callId = call.Id }, Envelope(call));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ErrorEnvelope("CALL_ALREADY_ACTIVE", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Joins an active video call.</summary>
    [HttpPost("calls/{callId:guid}/join")]
    public async Task<IActionResult> JoinCallAsync(Guid callId, [FromBody] JoinCallRequest request)
    {
        try
        {
            var call = await _videoCallService.JoinCallAsync(callId, request, GetAuthenticatedCaller());
            return Ok(Envelope(call));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CALL_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Leaves an active video call.</summary>
    [HttpPost("calls/{callId:guid}/leave")]
    public async Task<IActionResult> LeaveCallAsync(Guid callId)
    {
        try
        {
            await _videoCallService.LeaveCallAsync(callId, GetAuthenticatedCaller());
            return Ok(Envelope(new { left = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CALL_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Ends a video call for all participants.</summary>
    [HttpPost("calls/{callId:guid}/end")]
    public async Task<IActionResult> EndCallAsync(Guid callId)
    {
        try
        {
            await _videoCallService.EndCallAsync(callId, GetAuthenticatedCaller());
            return Ok(Envelope(new { ended = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CALL_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Rejects an incoming video call.</summary>
    [HttpPost("calls/{callId:guid}/reject")]
    public async Task<IActionResult> RejectCallAsync(Guid callId)
    {
        try
        {
            await _videoCallService.RejectCallAsync(callId, GetAuthenticatedCaller());
            return Ok(Envelope(new { rejected = true }));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope("CALL_NOT_FOUND", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Invites a user to join an active call. Only the call Host can invite participants.</summary>
    [HttpPost("calls/{callId:guid}/invite")]
    public async Task<IActionResult> InviteToCallAsync(Guid callId, [FromBody] InviteToCallRequest request)
    {
        try
        {
            await _videoCallService.InviteToCallAsync(callId, request.UserId, GetAuthenticatedCaller());
            return Ok(Envelope(new { invited = true }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorEnvelope("VALIDATION_ERROR", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ErrorEnvelope("INVITE_ERROR", ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Gets paginated call history for a channel.</summary>
    [HttpGet("channels/{channelId:guid}/calls")]
    public async Task<IActionResult> GetCallHistoryAsync(
        Guid channelId, [FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        try
        {
            take = Math.Clamp(take, 1, 100);
            skip = Math.Max(0, skip);
            var history = await _videoCallService.GetCallHistoryAsync(channelId, skip, take, GetAuthenticatedCaller());
            return Ok(Envelope(history));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Gets details for a specific video call.</summary>
    [HttpGet("calls/{callId:guid}", Name = "GetCall")]
    public async Task<IActionResult> GetCallAsync(Guid callId)
    {
        try
        {
            // Use the active call lookup by checking all channels; the service validates membership
            var caller = GetAuthenticatedCaller();
            var call = await _videoCallService.GetCallByIdAsync(callId, caller);
            if (call is null)
                return NotFound(ErrorEnvelope("CALL_NOT_FOUND", "Video call not found."));

            return Ok(Envelope(call));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Gets the active video call in a channel, if any.</summary>
    [HttpGet("channels/{channelId:guid}/calls/active")]
    public async Task<IActionResult> GetActiveCallAsync(Guid channelId)
    {
        try
        {
            var call = await _videoCallService.GetActiveCallAsync(channelId, GetAuthenticatedCaller());
            if (call is null)
                return Ok(new { success = true, data = (object?)null });

            return Ok(Envelope(call));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // ── ICE Server Configuration ────────────────────────────────────

    /// <summary>
    /// Returns ICE server configuration for WebRTC connections.
    /// Includes the built-in STUN server, any additional STUN servers,
    /// and TURN servers with ephemeral credentials when configured.
    /// </summary>
    [HttpGet("ice-servers")]
    public IActionResult GetIceServers()
    {
        GetAuthenticatedCaller(); // Ensure authenticated

        var publicHost = Request.Host.Host;
        var iceServers = _iceServerService.GetIceServers(publicHost);
        var transportPolicy = _iceServerService.IceTransportPolicy;

        return Ok(Envelope(new
        {
            iceServers,
            iceTransportPolicy = transportPolicy
        }));
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

/// <summary>DTO for registering a push notification device.</summary>
public sealed record RegisterDeviceRequestDto
{
    /// <summary>Push provider device token.</summary>
    public required string DeviceToken { get; init; }

    /// <summary>Provider value: FCM or UnifiedPush.</summary>
    public required string Provider { get; init; }

    /// <summary>UnifiedPush endpoint URL, when provider is UnifiedPush.</summary>
    public string? Endpoint { get; init; }
}

/// <summary>Caller-level notification preferences for push delivery.</summary>
public sealed record NotificationPreferencesDto
{
    /// <summary>Whether push notifications are globally enabled.</summary>
    public bool PushEnabled { get; init; } = true;

    /// <summary>Whether do-not-disturb mode is enabled.</summary>
    public bool DoNotDisturb { get; init; }

    /// <summary>Channel IDs muted for push notifications.</summary>
    public IReadOnlyList<Guid> MutedChannelIds { get; init; } = [];
}
