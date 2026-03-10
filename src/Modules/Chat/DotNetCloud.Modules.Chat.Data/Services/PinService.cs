using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Manages pinned messages in chat channels.
/// </summary>
internal sealed class PinService : IPinService
{
    private readonly ChatDbContext _db;
    private readonly ILogger<PinService> _logger;

    public PinService(ChatDbContext db, ILogger<PinService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PinMessageAsync(Guid channelId, Guid messageId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(channelId, cancellationToken);
        await EnsureCallerCanAccessChannelAsync(channelId, caller, cancellationToken);

        var messageExistsInChannel = await _db.Messages
            .AsNoTracking()
            .AnyAsync(m => m.Id == messageId && m.ChannelId == channelId, cancellationToken);

        if (!messageExistsInChannel)
            throw new InvalidOperationException($"Message {messageId} not found in channel {channelId}.");

        var exists = await _db.PinnedMessages
            .AnyAsync(p => p.ChannelId == channelId && p.MessageId == messageId, cancellationToken);

        if (exists)
            return;

        _db.PinnedMessages.Add(new PinnedMessage
        {
            ChannelId = channelId,
            MessageId = messageId,
            PinnedByUserId = caller.UserId
        });

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Message {MessageId} pinned in channel {ChannelId} by {UserId}", messageId, channelId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task UnpinMessageAsync(Guid channelId, Guid messageId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(channelId, cancellationToken);
        await EnsureCallerCanAccessChannelAsync(channelId, caller, cancellationToken);

        var pin = await _db.PinnedMessages
            .FirstOrDefaultAsync(p => p.ChannelId == channelId && p.MessageId == messageId, cancellationToken);

        if (pin is null)
            return;

        _db.PinnedMessages.Remove(pin);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Message {MessageId} unpinned from channel {ChannelId} by {UserId}", messageId, channelId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageDto>> GetPinnedMessagesAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(channelId, cancellationToken);
        await EnsureCallerCanAccessChannelAsync(channelId, caller, cancellationToken);

        var pinnedEntries = await _db.PinnedMessages
            .AsNoTracking()
            .Where(p => p.ChannelId == channelId)
            .OrderByDescending(p => p.PinnedAt)
            .ToListAsync(cancellationToken);

        var pinnedMessageIds = pinnedEntries.Select(p => p.MessageId).ToList();

        if (pinnedMessageIds.Count == 0)
            return [];

        var messages = await _db.Messages
            .AsNoTracking()
            .Where(m => pinnedMessageIds.Contains(m.Id))
            .Include(m => m.Attachments)
            .Include(m => m.Reactions)
            .Include(m => m.Mentions)
            .ToListAsync(cancellationToken);

        var messageMap = messages.ToDictionary(m => m.Id);

        return pinnedEntries
            .Select(p => messageMap.TryGetValue(p.MessageId, out var message) ? ToMessageDto(message) : null)
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();
    }

    private static MessageDto ToMessageDto(Message m)
    {
        return new MessageDto
        {
            Id = m.Id,
            ChannelId = m.ChannelId,
            SenderUserId = m.SenderUserId,
            Content = m.Content,
            Type = m.Type.ToString(),
            SentAt = m.SentAt,
            EditedAt = m.EditedAt,
            IsEdited = m.IsEdited,
            ReplyToMessageId = m.ReplyToMessageId,
            Attachments = m.Attachments.Select(a => new MessageAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                MimeType = a.MimeType,
                FileSize = a.FileSize,
                ThumbnailUrl = a.ThumbnailUrl,
                FileNodeId = a.FileNodeId
            }).ToList(),
            Reactions = m.Reactions
                .GroupBy(r => r.Emoji)
                .Select(g => new MessageReactionDto
                {
                    Emoji = g.Key,
                    Count = g.Count(),
                    UserIds = g.Select(r => r.UserId).ToList()
                }).ToList(),
            Mentions = m.Mentions.Select(mm => new MessageMentionDto
            {
                Type = mm.Type.ToString(),
                MentionedUserId = mm.MentionedUserId,
                StartIndex = mm.StartIndex,
                Length = mm.Length
            }).ToList()
        };
    }

    private async Task EnsureChannelExistsAsync(Guid channelId, CancellationToken cancellationToken)
    {
        var exists = await _db.Channels
            .AsNoTracking()
            .AnyAsync(c => c.Id == channelId, cancellationToken);

        if (!exists)
            throw new InvalidOperationException($"Channel {channelId} not found.");
    }

    private async Task EnsureCallerCanAccessChannelAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken)
    {
        if (caller.Type == CallerType.System)
            return;

        var isMember = await _db.ChannelMembers
            .AsNoTracking()
            .AnyAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken);

        if (!isMember)
        {
            _logger.LogWarning(
                "Denied pin action. ChannelId={ChannelId} CallerUserId={CallerUserId}",
                channelId,
                caller.UserId);
            throw new UnauthorizedAccessException($"User {caller.UserId} is not a member of channel {channelId}.");
        }
    }
}
