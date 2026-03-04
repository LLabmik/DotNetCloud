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
        if (string.IsNullOrWhiteSpace(emoji))
            throw new ArgumentException("Emoji is required.", nameof(emoji));

        var message = await _db.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
            ?? throw new InvalidOperationException($"Message {messageId} not found.");

        var exists = await _db.MessageReactions
            .AnyAsync(r => r.MessageId == messageId && r.UserId == caller.UserId && r.Emoji == emoji, cancellationToken);

        if (exists)
            return;

        _db.MessageReactions.Add(new MessageReaction
        {
            MessageId = messageId,
            UserId = caller.UserId,
            Emoji = emoji
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ReactionAddedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageId = messageId,
            ChannelId = message.ChannelId,
            UserId = caller.UserId,
            Emoji = emoji
        }, caller, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveReactionAsync(Guid messageId, string emoji, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var reaction = await _db.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == caller.UserId && r.Emoji == emoji, cancellationToken);

        if (reaction is null)
            return;

        var message = await _db.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        _db.MessageReactions.Remove(reaction);
        await _db.SaveChangesAsync(cancellationToken);

        if (message is not null)
        {
            await _eventBus.PublishAsync(new ReactionRemovedEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                MessageId = messageId,
                ChannelId = message.ChannelId,
                UserId = caller.UserId,
                Emoji = emoji
            }, caller, cancellationToken);
        }
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
}
