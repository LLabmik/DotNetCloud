using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing cards (work items) on boards.
/// </summary>
public sealed class CardService
{
    private const double PositionGap = 1000.0;

    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CardService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardService"/> class.
    /// </summary>
    public CardService(TracksDbContext db, BoardService boardService, ActivityService activityService, IEventBus eventBus, ILogger<CardService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new card in a list. Requires Member role or higher. Enforces WIP limits.
    /// </summary>
    public async Task<CardDto> CreateCardAsync(Guid listId, CreateCardDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var list = await _db.BoardLists
            .FirstOrDefaultAsync(l => l.Id == listId && !l.IsArchived, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardListNotFound, "List not found.");

        await _boardService.EnsureBoardRoleAsync(list.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        // WIP limit check
        if (list.CardLimit.HasValue)
        {
            var activeCount = await _db.Cards
                .CountAsync(c => c.ListId == listId && !c.IsDeleted && !c.IsArchived, cancellationToken);

            if (activeCount >= list.CardLimit.Value)
                throw new ValidationException(ErrorCodes.WipLimitExceeded,
                    $"List '{list.Title}' has reached its WIP limit of {list.CardLimit.Value}.");
        }

        // Position: append after last card
        var maxPos = await _db.Cards
            .Where(c => c.ListId == listId && !c.IsDeleted)
            .MaxAsync(c => (double?)c.Position, cancellationToken) ?? 0;

        // Assign next sequential card number (globally unique across the system)
        var maxCardNumber = await _db.Cards
            .IgnoreQueryFilters()
            .MaxAsync(c => (int?)c.CardNumber, cancellationToken) ?? 0;

        var card = new Card
        {
            ListId = listId,
            CardNumber = maxCardNumber + 1,
            Title = dto.Title,
            Description = dto.Description,
            Priority = dto.Priority,
            DueDate = dto.DueDate,
            StoryPoints = dto.StoryPoints,
            Position = maxPos + PositionGap,
            CreatedByUserId = caller.UserId
        };

        // Assignments
        foreach (var userId in dto.AssigneeIds)
        {
            card.Assignments.Add(new CardAssignment
            {
                CardId = card.Id,
                UserId = userId,
                AssignedAt = DateTime.UtcNow
            });
        }

        // Labels
        foreach (var labelId in dto.LabelIds)
        {
            card.CardLabels.Add(new CardLabel
            {
                CardId = card.Id,
                LabelId = labelId,
                AppliedAt = DateTime.UtcNow
            });
        }

        _db.Cards.Add(card);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Card {CardId} '{Title}' created in list {ListId} by user {UserId}",
            card.Id, card.Title, listId, caller.UserId);

        await _activityService.LogAsync(list.BoardId, caller.UserId, "card.created", "Card", card.Id,
            $"{{\"title\":\"{card.Title}\",\"listId\":\"{listId}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new CardCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = card.Id,
            Title = card.Title,
            BoardId = list.BoardId,
            ListId = listId,
            CreatedByUserId = caller.UserId
        }, caller, cancellationToken);

        return await GetCardDtoAsync(card.Id, cancellationToken)
            ?? throw new System.InvalidOperationException("Card was created but could not be retrieved.");
    }

    /// <summary>
    /// Gets a card by ID.
    /// </summary>
    public async Task<CardDto?> GetCardAsync(Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .AsNoTracking()
            .Include(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken);

        if (card is null)
            return null;

        await _boardService.EnsureBoardMemberAsync(card.List!.BoardId, caller.UserId, cancellationToken);

        return await GetCardDtoAsync(cardId, cancellationToken);
    }

    /// <summary>
    /// Lists cards in a list, ordered by position.
    /// </summary>
    public async Task<IReadOnlyList<CardDto>> ListCardsAsync(Guid listId, CallerContext caller, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var list = await _db.BoardLists
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == listId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardListNotFound, "List not found.");

        await _boardService.EnsureBoardMemberAsync(list.BoardId, caller.UserId, cancellationToken);

        var query = _db.Cards
            .AsNoTracking()
            .Include(c => c.Assignments)
            .Include(c => c.CardLabels).ThenInclude(cl => cl.Label)
            .Include(c => c.Checklists).ThenInclude(ch => ch.Items)
            .Where(c => c.ListId == listId && !c.IsDeleted);

        if (!includeArchived)
            query = query.Where(c => !c.IsArchived);

        var cards = await query
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);

        return cards.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Updates a card. Requires Member role or higher.
    /// </summary>
    public async Task<CardDto> UpdateCardAsync(Guid cardId, UpdateCardDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var card = await _db.Cards
            .Include(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        if (dto.Title is not null) card.Title = dto.Title;
        if (dto.Description is not null) card.Description = dto.Description;
        if (dto.Priority.HasValue) card.Priority = dto.Priority.Value;
        if (dto.DueDate.HasValue) card.DueDate = dto.DueDate.Value;
        if (dto.StoryPoints.HasValue) card.StoryPoints = dto.StoryPoints.Value;
        if (dto.IsArchived.HasValue) card.IsArchived = dto.IsArchived.Value;

        card.UpdatedAt = DateTime.UtcNow;
        card.ETag = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Card {CardId} updated by user {UserId}", cardId, caller.UserId);

        await _activityService.LogAsync(card.List.BoardId, caller.UserId, "card.updated", "Card", cardId, null, cancellationToken);

        await _eventBus.PublishAsync(new CardUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = cardId,
            BoardId = card.List.BoardId,
            UpdatedByUserId = caller.UserId
        }, caller, cancellationToken);

        return await GetCardDtoAsync(cardId, cancellationToken)
            ?? throw new System.InvalidOperationException("Card was updated but could not be retrieved.");
    }

    /// <summary>
    /// Moves a card to a different list and/or position. Enforces WIP limits.
    /// </summary>
    public async Task<CardDto> MoveCardAsync(Guid cardId, MoveCardDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var card = await _db.Cards
            .Include(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var targetList = await _db.BoardLists
            .FirstOrDefaultAsync(l => l.Id == dto.TargetListId && !l.IsArchived, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardListNotFound, "Target list not found.");

        // WIP limit check on target list (skip if moving within same list)
        if (card.ListId != dto.TargetListId && targetList.CardLimit.HasValue)
        {
            var activeCount = await _db.Cards
                .CountAsync(c => c.ListId == dto.TargetListId && !c.IsDeleted && !c.IsArchived, cancellationToken);

            if (activeCount >= targetList.CardLimit.Value)
                throw new ValidationException(ErrorCodes.WipLimitExceeded,
                    $"Target list '{targetList.Title}' has reached its WIP limit of {targetList.CardLimit.Value}.");
        }

        var fromListId = card.ListId;
        card.ListId = dto.TargetListId;
        card.Position = dto.Position;
        card.UpdatedAt = DateTime.UtcNow;
        card.ETag = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Card {CardId} moved from list {FromListId} to {ToListId} by user {UserId}",
            cardId, fromListId, dto.TargetListId, caller.UserId);

        await _activityService.LogAsync(card.List.BoardId, caller.UserId, "card.moved", "Card", cardId,
            $"{{\"from\":\"{fromListId}\",\"to\":\"{dto.TargetListId}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new CardMovedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = cardId,
            BoardId = card.List.BoardId,
            FromListId = fromListId,
            ToListId = dto.TargetListId,
            MovedByUserId = caller.UserId
        }, caller, cancellationToken);

        return await GetCardDtoAsync(cardId, cancellationToken)
            ?? throw new System.InvalidOperationException("Card was moved but could not be retrieved.");
    }

    /// <summary>
    /// Soft-deletes a card. Requires Member role or higher.
    /// </summary>
    public async Task DeleteCardAsync(Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .Include(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        card.IsDeleted = true;
        card.DeletedAt = DateTime.UtcNow;
        card.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Card {CardId} deleted by user {UserId}", cardId, caller.UserId);

        await _activityService.LogAsync(card.List.BoardId, caller.UserId, "card.deleted", "Card", cardId,
            $"{{\"title\":\"{card.Title}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new CardDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = cardId,
            BoardId = card.List.BoardId,
            DeletedByUserId = caller.UserId,
            IsPermanent = false
        }, caller, cancellationToken);
    }

    /// <summary>
    /// Assigns a user to a card. Requires Member role or higher.
    /// </summary>
    public async Task AssignUserAsync(Guid cardId, Guid userId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .Include(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        // Target user must be a board member
        await _boardService.EnsureBoardMemberAsync(card.List.BoardId, userId, cancellationToken);

        var alreadyAssigned = await _db.CardAssignments
            .AnyAsync(a => a.CardId == cardId && a.UserId == userId, cancellationToken);

        if (alreadyAssigned)
            return; // Idempotent

        _db.CardAssignments.Add(new CardAssignment
        {
            CardId = cardId,
            UserId = userId,
            AssignedAt = DateTime.UtcNow
        });

        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _activityService.LogAsync(card.List.BoardId, caller.UserId, "card.assigned", "Card", cardId,
            $"{{\"userId\":\"{userId}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new CardAssignedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = cardId,
            BoardId = card.List.BoardId,
            AssignedUserId = userId,
            AssignedByUserId = caller.UserId
        }, caller, cancellationToken);
    }

    /// <summary>
    /// Unassigns a user from a card. Requires Member role or higher.
    /// </summary>
    public async Task UnassignUserAsync(Guid cardId, Guid userId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .Include(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var assignment = await _db.CardAssignments
            .FirstOrDefaultAsync(a => a.CardId == cardId && a.UserId == userId, cancellationToken);

        if (assignment is null)
            return; // Idempotent

        _db.CardAssignments.Remove(assignment);
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _activityService.LogAsync(card.List.BoardId, caller.UserId, "card.unassigned", "Card", cardId,
            $"{{\"userId\":\"{userId}\"}}", cancellationToken);
    }

    private async Task<CardDto?> GetCardDtoAsync(Guid cardId, CancellationToken cancellationToken)
    {
        var card = await _db.Cards
            .AsNoTracking()
            .Include(c => c.List)
            .Include(c => c.Assignments)
            .Include(c => c.CardLabels).ThenInclude(cl => cl.Label)
            .Include(c => c.Checklists).ThenInclude(ch => ch.Items)
            .Include(c => c.Comments.Where(cm => !cm.IsDeleted))
            .Include(c => c.Attachments)
            .Include(c => c.TimeEntries)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken);

        return card is null ? null : MapToDto(card);
    }

    internal static CardDto MapToDto(Card c) => new()
    {
        Id = c.Id,
        ListId = c.ListId,
        BoardId = c.List?.BoardId ?? Guid.Empty,
        CardNumber = c.CardNumber,
        Title = c.Title,
        Description = c.Description,
        Position = (int)c.Position,
        Priority = c.Priority,
        DueDate = c.DueDate,
        StoryPoints = c.StoryPoints,
        IsArchived = c.IsArchived,
        IsDeleted = c.IsDeleted,
        DeletedAt = c.DeletedAt,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        ETag = c.ETag,
        Assignments = c.Assignments.Select(a => new CardAssignmentDto
        {
            UserId = a.UserId,
            AssignedAt = a.AssignedAt
        }).ToList(),
        Labels = c.CardLabels.Where(cl => cl.Label is not null).Select(cl => new LabelDto
        {
            Id = cl.Label!.Id,
            BoardId = cl.Label.BoardId,
            Title = cl.Label.Title,
            Color = cl.Label.Color
        }).ToList(),
        Checklists = c.Checklists.OrderBy(ch => ch.Position).Select(ch => new CardChecklistDto
        {
            Id = ch.Id,
            CardId = ch.CardId,
            Title = ch.Title,
            Position = (int)ch.Position,
            Items = ch.Items.OrderBy(i => i.Position).Select(i => new ChecklistItemDto
            {
                Id = i.Id,
                Title = i.Title,
                IsCompleted = i.IsCompleted,
                Position = (int)i.Position
            }).ToList()
        }).ToList(),
        CommentCount = c.Comments.Count(cm => !cm.IsDeleted),
        AttachmentCount = c.Attachments.Count,
        TotalTrackedMinutes = c.TimeEntries.Sum(t => t.DurationMinutes)
    };
}
