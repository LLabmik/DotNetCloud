using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Manages emoji reactions on chat messages.
/// </summary>
internal sealed class ReactionService : IReactionService
{
    private readonly ChatDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ReactionService> _logger;

    public ReactionService(ChatDbContext db, IEventBus eventBus, ILogger<ReactionService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddReactionAsync(Guid messageId, string emoji, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var normalizedEmoji = emoji?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmoji))
            throw new ArgumentException("Emoji is required.", nameof(emoji));

        var message = await _db.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
            ?? throw new InvalidOperationException($"Message {messageId} not found.");

        await EnsureCallerCanAccessChannelAsync(message.ChannelId, caller, cancellationToken);

        var exists = await _db.MessageReactions
            .AnyAsync(r => r.MessageId == messageId && r.UserId == caller.UserId && r.Emoji == normalizedEmoji, cancellationToken);

        if (exists)
            return;

        _db.MessageReactions.Add(new MessageReaction
        {
            MessageId = messageId,
            UserId = caller.UserId,
            Emoji = normalizedEmoji
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ReactionAddedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageId = messageId,
            ChannelId = message.ChannelId,
            UserId = caller.UserId,
            Emoji = normalizedEmoji
        }, caller, cancellationToken);

        _logger.LogInformation(
            "Reaction added. MessageId={MessageId} ChannelId={ChannelId} UserId={UserId} Emoji={Emoji}",
            messageId,
            message.ChannelId,
            caller.UserId,
            normalizedEmoji);
    }

    /// <inheritdoc />
    public async Task RemoveReactionAsync(Guid messageId, string emoji, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var normalizedEmoji = emoji?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmoji))
            throw new ArgumentException("Emoji is required.", nameof(emoji));

        var message = await _db.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
            ?? throw new InvalidOperationException($"Message {messageId} not found.");

        await EnsureCallerCanAccessChannelAsync(message.ChannelId, caller, cancellationToken);

        var reaction = await _db.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == caller.UserId && r.Emoji == normalizedEmoji, cancellationToken);

        if (reaction is null)
            return;

        _db.MessageReactions.Remove(reaction);
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ReactionRemovedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageId = messageId,
            ChannelId = message.ChannelId,
            UserId = caller.UserId,
            Emoji = normalizedEmoji
        }, caller, cancellationToken);

        _logger.LogInformation(
            "Reaction removed. MessageId={MessageId} ChannelId={ChannelId} UserId={UserId} Emoji={Emoji}",
            messageId,
            message.ChannelId,
            caller.UserId,
            normalizedEmoji);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageReactionDto>> GetReactionsAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var reactions = await _db.MessageReactions
            .AsNoTracking()
            .Where(r => r.MessageId == messageId)
            .ToListAsync(cancellationToken);

        return reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new MessageReactionDto
            {
                Emoji = g.Key,
                Count = g.Count(),
                UserIds = g.Select(r => r.UserId).ToList()
            }).ToList();
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
                "Denied reaction action. ChannelId={ChannelId} MessageActorUserId={CallerUserId}",
                channelId,
                caller.UserId);
            throw new UnauthorizedAccessException($"User {caller.UserId} is not a member of channel {channelId}.");
        }
    }
}
