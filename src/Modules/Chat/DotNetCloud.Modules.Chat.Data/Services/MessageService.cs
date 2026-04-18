using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IUserDirectory = DotNetCloud.Core.Capabilities.IUserDirectory;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Manages chat messages: sending, editing, deleting, searching, and mention parsing.
/// </summary>
internal sealed class MessageService : IMessageService
{
    private readonly ChatDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IUserDirectory? _userDirectory;
    private readonly IMentionNotificationService? _mentionNotifier;
    private readonly IUserBlockService? _userBlockService;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        ChatDbContext db,
        IEventBus eventBus,
        ILogger<MessageService> logger,
        IUserDirectory? userDirectory = null,
        IMentionNotificationService? mentionNotifier = null,
        IUserBlockService? userBlockService = null)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
        _userDirectory = userDirectory;
        _mentionNotifier = mentionNotifier;
        _userBlockService = userBlockService;
    }

    /// <inheritdoc />
    public async Task<MessageDto> SendMessageAsync(Guid channelId, SendMessageDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Content))
            throw new ArgumentException("Message content is required.", nameof(dto));

        var isMember = await _db.ChannelMembers
            .AnyAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken);

        if (!isMember)
            throw new UnauthorizedAccessException($"User {caller.UserId} is not a member of channel {channelId}.");

        // For DM channels, check if the other user has blocked the sender
        if (_userBlockService is not null)
        {
            var ch = await _db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == channelId, cancellationToken);
            if (ch?.Type == Models.ChannelType.DirectMessage)
            {
                var otherMember = await _db.ChannelMembers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId != caller.UserId, cancellationToken);
                if (otherMember is not null)
                {
                    var isBlocked = await _userBlockService.IsBlockedAsync(caller.UserId, otherMember.UserId, cancellationToken);
                    if (isBlocked)
                        throw new InvalidOperationException("You have been blocked by this user. You cannot send messages.");
                }
            }
        }

        var message = new Message
        {
            ChannelId = channelId,
            SenderUserId = caller.UserId,
            Content = dto.Content,
            Type = dto.ReplyToMessageId.HasValue ? MessageType.Reply : MessageType.Text,
            ReplyToMessageId = dto.ReplyToMessageId
        };

        _db.Messages.Add(message);

        // Parse and store mentions
        var mentions = await ParseAndStoreMentionsAsync(message, cancellationToken);

        // Update channel activity
        var channel = await _db.Channels.FindAsync([channelId], cancellationToken);
        if (channel is not null)
        {
            channel.LastActivityAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new MessageSentEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageId = message.Id,
            ChannelId = channelId,
            SenderUserId = caller.UserId,
            Content = message.Content,
            MessageType = message.Type.ToString()
        }, caller, cancellationToken);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "chat",
            EntityId = message.Id.ToString(),
            Action = SearchIndexAction.Index
        }, caller, cancellationToken);

        // Dispatch mention notifications after the message is persisted
        if (mentions.Count > 0 && _mentionNotifier is not null)
        {
            await _mentionNotifier.DispatchMentionNotificationsAsync(
                message.Id, channelId, caller.UserId, mentions, cancellationToken);
        }

        return ToMessageDto(message);
    }

    /// <inheritdoc />
    public async Task<MessageDto> EditMessageAsync(Guid messageId, EditMessageDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Content))
            throw new ArgumentException("Message content is required.", nameof(dto));

        var message = await _db.Messages
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
            ?? throw new InvalidOperationException($"Message {messageId} not found.");

        if (message.SenderUserId != caller.UserId)
            throw new UnauthorizedAccessException("Only the message sender can edit a message.");

        message.Content = dto.Content;
        message.EditedAt = DateTime.UtcNow;
        message.IsEdited = true;

        // Re-parse mentions
        var oldMentions = await _db.MessageMentions
            .Where(m => m.MessageId == messageId)
            .ToListAsync(cancellationToken);
        _db.MessageMentions.RemoveRange(oldMentions);
        var newMentions = await ParseAndStoreMentionsAsync(message, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new MessageEditedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageId = message.Id,
            ChannelId = message.ChannelId,
            EditedByUserId = caller.UserId,
            NewContent = message.Content
        }, caller, cancellationToken);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "chat",
            EntityId = message.Id.ToString(),
            Action = SearchIndexAction.Index
        }, caller, cancellationToken);

        // Dispatch mention notifications for new mentions after edit
        if (newMentions.Count > 0 && _mentionNotifier is not null)
        {
            await _mentionNotifier.DispatchMentionNotificationsAsync(
                message.Id, message.ChannelId, caller.UserId, newMentions, cancellationToken);
        }

        return ToMessageDto(message);
    }

    /// <inheritdoc />
    public async Task DeleteMessageAsync(Guid messageId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages.FindAsync([messageId], cancellationToken)
            ?? throw new InvalidOperationException($"Message {messageId} not found.");

        // Sender or channel admin/owner can delete
        if (message.SenderUserId != caller.UserId)
        {
            var membership = await _db.ChannelMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ChannelId == message.ChannelId && m.UserId == caller.UserId, cancellationToken);

            if (membership is null || membership.Role == ChannelMemberRole.Member)
                throw new UnauthorizedAccessException("Only the message sender or a channel admin/owner can delete a message.");
        }

        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new MessageDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageId = message.Id,
            ChannelId = message.ChannelId,
            DeletedByUserId = caller.UserId
        }, caller, cancellationToken);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "chat",
            EntityId = message.Id.ToString(),
            Action = SearchIndexAction.Remove
        }, caller, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedMessageResult> GetMessagesAsync(Guid channelId, int page, int pageSize, CallerContext caller, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Messages
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId)
            .OrderByDescending(m => m.SentAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var messages = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions)
            .Include(m => m.Mentions)
            .ToListAsync(cancellationToken);

        return new PagedMessageResult
        {
            Items = messages.Select(ToMessageDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalCount
        };
    }

    /// <inheritdoc />
    public async Task<PagedMessageResult> SearchMessagesAsync(Guid channelId, string query, int page, int pageSize, CallerContext caller, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var dbQuery = _db.Messages
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId && m.Content.Contains(query))
            .OrderByDescending(m => m.SentAt);

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var messages = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions)
            .Include(m => m.Mentions)
            .ToListAsync(cancellationToken);

        return new PagedMessageResult
        {
            Items = messages.Select(ToMessageDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalCount
        };
    }

    /// <inheritdoc />
    public async Task<MessageDto?> GetMessageAsync(Guid messageId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages
            .AsNoTracking()
            .Include(m => m.Attachments)
            .Include(m => m.Reactions)
            .Include(m => m.Mentions)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        return message is null ? null : ToMessageDto(message);
    }

    /// <inheritdoc />
    public async Task<MessageAttachmentDto> AddAttachmentAsync(Guid channelId, Guid messageId, CreateAttachmentDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.FileName))
            throw new ArgumentException("File name is required.", nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.MimeType))
            throw new ArgumentException("MIME type is required.", nameof(dto));

        var message = await _db.Messages
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ChannelId == channelId, cancellationToken)
            ?? throw new InvalidOperationException($"Message {messageId} not found in channel {channelId}.");

        if (message.SenderUserId != caller.UserId)
            throw new UnauthorizedAccessException("Only the message sender can add attachments.");

        var nextSortOrder = message.Attachments.Count > 0
            ? message.Attachments.Max(a => a.SortOrder) + 1
            : 0;

        var attachment = new MessageAttachment
        {
            MessageId = messageId,
            FileName = dto.FileName,
            MimeType = dto.MimeType,
            FileSize = dto.FileSize,
            ThumbnailUrl = dto.ThumbnailUrl,
            FileNodeId = dto.FileNodeId,
            SortOrder = nextSortOrder
        };

        _db.MessageAttachments.Add(attachment);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment {AttachmentId} added to message {MessageId} in channel {ChannelId} by user {UserId}.",
            attachment.Id, messageId, channelId, caller.UserId);

        return new MessageAttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            MimeType = attachment.MimeType,
            FileSize = attachment.FileSize,
            ThumbnailUrl = attachment.ThumbnailUrl,
            FileNodeId = attachment.FileNodeId
        };
    }

    /// <summary>
    /// Parses @mentions from message content and stores them in the database.
    /// Supports @all, @channel, and @username mentions.
    /// </summary>
    private async Task<IReadOnlyList<MessageMention>> ParseAndStoreMentionsAsync(
        Message message, CancellationToken cancellationToken)
    {
        var content = message.Content;
        var index = 0;
        var mentions = new List<MessageMention>();

        while (index < content.Length)
        {
            var atIndex = content.IndexOf('@', index);
            if (atIndex < 0)
                break;

            var endIndex = atIndex + 1;
            while (endIndex < content.Length && !char.IsWhiteSpace(content[endIndex]) && content[endIndex] != '@')
                endIndex++;

            var mentionText = content[atIndex..endIndex];
            var length = endIndex - atIndex;

            if (string.Equals(mentionText, "@all", StringComparison.OrdinalIgnoreCase))
            {
                var mention = new MessageMention
                {
                    MessageId = message.Id,
                    Type = MentionType.All,
                    StartIndex = atIndex,
                    Length = length
                };
                _db.MessageMentions.Add(mention);
                mentions.Add(mention);
            }
            else if (string.Equals(mentionText, "@channel", StringComparison.OrdinalIgnoreCase))
            {
                var mention = new MessageMention
                {
                    MessageId = message.Id,
                    Type = MentionType.Channel,
                    StartIndex = atIndex,
                    Length = length
                };
                _db.MessageMentions.Add(mention);
                mentions.Add(mention);
            }
            else if (mentionText.Length > 1 && _userDirectory is not null)
            {
                // Resolve @username against the user directory
                var username = mentionText[1..]; // strip the '@'
                var userId = await _userDirectory.FindUserIdByUsernameAsync(username, cancellationToken);
                if (userId.HasValue)
                {
                    var mention = new MessageMention
                    {
                        MessageId = message.Id,
                        MentionedUserId = userId.Value,
                        Type = MentionType.User,
                        StartIndex = atIndex,
                        Length = length
                    };
                    _db.MessageMentions.Add(mention);
                    mentions.Add(mention);
                }
            }

            index = endIndex;
        }

        return mentions;
    }

    private static MessageDto ToMessageDto(Message message)
    {
        return new MessageDto
        {
            Id = message.Id,
            ChannelId = message.ChannelId,
            SenderUserId = message.SenderUserId,
            Content = message.Content,
            Type = message.Type.ToString(),
            SentAt = message.SentAt,
            EditedAt = message.EditedAt,
            IsEdited = message.IsEdited,
            ReplyToMessageId = message.ReplyToMessageId,
            Attachments = message.Attachments.Select(a => new MessageAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                MimeType = a.MimeType,
                FileSize = a.FileSize,
                ThumbnailUrl = a.ThumbnailUrl,
                FileNodeId = a.FileNodeId
            }).ToList(),
            Reactions = message.Reactions
                .GroupBy(r => r.Emoji)
                .Select(g => new MessageReactionDto
                {
                    Emoji = g.Key,
                    Count = g.Count(),
                    UserIds = g.Select(r => r.UserId).ToList()
                }).ToList(),
            Mentions = message.Mentions.Select(m => new MessageMentionDto
            {
                Type = m.Type.ToString(),
                MentionedUserId = m.MentionedUserId,
                StartIndex = m.StartIndex,
                Length = m.Length
            }).ToList()
        };
    }
}
