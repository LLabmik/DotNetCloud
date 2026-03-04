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
        var pinnedMessageIds = await _db.PinnedMessages
            .AsNoTracking()
            .Where(p => p.ChannelId == channelId)
            .OrderByDescending(p => p.PinnedAt)
            .Select(p => p.MessageId)
            .ToListAsync(cancellationToken);

        var messages = await _db.Messages
            .AsNoTracking()
            .Where(m => pinnedMessageIds.Contains(m.Id))
            .Include(m => m.Attachments)
            .Include(m => m.Reactions)
            .ToListAsync(cancellationToken);

        return messages.Select(m => new MessageDto
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
                }).ToList()
        }).ToList();
    }
}
