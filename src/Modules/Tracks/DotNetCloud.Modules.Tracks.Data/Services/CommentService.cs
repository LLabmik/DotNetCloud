using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing card comments with Markdown support.
/// </summary>
public sealed class CommentService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CommentService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentService"/> class.
    /// </summary>
    public CommentService(TracksDbContext db, BoardService boardService, ActivityService activityService, IEventBus eventBus, ILogger<CommentService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Adds a comment to a card. Requires Member role or higher.
    /// </summary>
    public async Task<CardCommentDto> CreateCommentAsync(Guid cardId, string content, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var card = await _db.Cards
            .Include(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var comment = new CardComment
        {
            CardId = cardId,
            UserId = caller.UserId,
            Content = content
        };

        _db.CardComments.Add(comment);
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Comment {CommentId} added to card {CardId} by user {UserId}",
            comment.Id, cardId, caller.UserId);

        await _activityService.LogAsync(card.List.BoardId, caller.UserId, "comment.added", "CardComment", comment.Id,
            $"{{\"cardId\":\"{cardId}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new CardCommentAddedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CommentId = comment.Id,
            CardId = cardId,
            BoardId = card.List.BoardId,
            UserId = caller.UserId
        }, caller, cancellationToken);

        return MapToDto(comment);
    }

    /// <summary>
    /// Gets all comments for a card, ordered by creation date.
    /// </summary>
    public async Task<IReadOnlyList<CardCommentDto>> GetCommentsAsync(Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .AsNoTracking()
            .Include(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardMemberAsync(card.List!.BoardId, caller.UserId, cancellationToken);

        var comments = await _db.CardComments
            .AsNoTracking()
            .Where(c => c.CardId == cardId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return comments.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Updates a comment. Only the comment author can edit.
    /// </summary>
    public async Task<CardCommentDto> UpdateCommentAsync(Guid commentId, string content, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var comment = await _db.CardComments
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CommentNotFound, "Comment not found.");

        if (comment.UserId != caller.UserId)
            throw new ValidationException(ErrorCodes.InsufficientBoardRole, "Only the comment author can edit.");

        comment.Content = content;
        comment.IsEdited = true;
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Comment {CommentId} updated by user {UserId}", commentId, caller.UserId);

        return MapToDto(comment);
    }

    /// <summary>
    /// Soft-deletes a comment. The author or an Admin can delete.
    /// </summary>
    public async Task DeleteCommentAsync(Guid commentId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var comment = await _db.CardComments
            .Include(c => c.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CommentNotFound, "Comment not found.");

        // Author can always delete their own comment; otherwise requires Admin
        if (comment.UserId != caller.UserId)
        {
            await _boardService.EnsureBoardRoleAsync(comment.Card!.List!.BoardId, caller.UserId,
                BoardMemberRole.Admin, cancellationToken);
        }

        comment.IsDeleted = true;
        comment.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Comment {CommentId} deleted by user {UserId}", commentId, caller.UserId);
    }

    private static CardCommentDto MapToDto(CardComment c) => new()
    {
        Id = c.Id,
        CardId = c.CardId,
        UserId = c.UserId,
        Content = c.Content,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
