using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IUserDirectory = DotNetCloud.Core.Capabilities.IUserDirectory;

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
    private readonly IUserDirectory? _userDirectory;
    private readonly ILogger<CardService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardService"/> class.
    /// </summary>
    public CardService(TracksDbContext db, BoardService boardService, ActivityService activityService, IEventBus eventBus, ILogger<CardService> logger, IUserDirectory? userDirectory = null)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _eventBus = eventBus;
        _userDirectory = userDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new card in a swimlane. Requires Member role or higher. Enforces WIP limits.
    /// </summary>
    public async Task<CardDto> CreateCardAsync(Guid swimlaneId, CreateCardDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var swimlane = await _db.BoardSwimlanes
            .FirstOrDefaultAsync(l => l.Id == swimlaneId && !l.IsArchived, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardSwimlaneNotFound, "Swimlane not found.");

        await _boardService.EnsureBoardRoleAsync(swimlane.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        // WIP limit check
        if (swimlane.CardLimit.HasValue)
        {
            var activeCount = await _db.Cards
                .CountAsync(c => c.SwimlaneId == swimlaneId && !c.IsDeleted && !c.IsArchived, cancellationToken);

            if (activeCount >= swimlane.CardLimit.Value)
                throw new ValidationException(ErrorCodes.WipLimitExceeded,
                    $"Swimlane '{swimlane.Title}' has reached its WIP limit of {swimlane.CardLimit.Value}.");
        }

        // Position: append after last card
        var maxPos = await _db.Cards
            .Where(c => c.SwimlaneId == swimlaneId && !c.IsDeleted)
            .MaxAsync(c => (double?)c.Position, cancellationToken) ?? 0;

        // Assign next sequential card number (globally unique across the system)
        var maxCardNumber = await _db.Cards
            .IgnoreQueryFilters()
            .MaxAsync(c => (int?)c.CardNumber, cancellationToken) ?? 0;

        var card = new Card
        {
            SwimlaneId = swimlaneId,
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

        _logger.LogInformation("Card {CardId} '{Title}' created in swimlane {SwimlaneId} by user {UserId}",
            card.Id, card.Title, swimlaneId, caller.UserId);

        await _activityService.LogAsync(swimlane.BoardId, caller.UserId, "card.created", "Card", card.Id,
            $"{{\"title\":\"{card.Title}\",\"swimlaneId\":\"{swimlaneId}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new CardCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = card.Id,
            Title = card.Title,
            BoardId = swimlane.BoardId,
            SwimlaneId = swimlaneId,
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
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken);

        if (card is null)
            return null;

        await _boardService.EnsureBoardMemberAsync(card.Swimlane!.BoardId, caller.UserId, cancellationToken);

        return await GetCardDtoAsync(cardId, cancellationToken);
    }

    /// <summary>
    /// Gets a card by its human-readable card number.
    /// </summary>
    public async Task<CardDto?> GetCardByNumberAsync(int cardNumber, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .AsNoTracking()
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.CardNumber == cardNumber && !c.IsDeleted, cancellationToken);

        if (card is null)
            return null;

        await _boardService.EnsureBoardMemberAsync(card.Swimlane!.BoardId, caller.UserId, cancellationToken);

        return await GetCardDtoAsync(card.Id, cancellationToken);
    }

    /// <summary>
    /// Lists cards in a swimlane, ordered by position.
    /// </summary>
    public async Task<IReadOnlyList<CardDto>> ListCardsAsync(Guid swimlaneId, CallerContext caller, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var swimlane = await _db.BoardSwimlanes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == swimlaneId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardSwimlaneNotFound, "Swimlane not found.");

        await _boardService.EnsureBoardMemberAsync(swimlane.BoardId, caller.UserId, cancellationToken);

        var query = _db.Cards
            .AsNoTracking()
            .Include(c => c.Assignments)
            .Include(c => c.CardLabels).ThenInclude(cl => cl.Label)
            .Include(c => c.Checklists).ThenInclude(ch => ch.Items)
            .Include(c => c.Comments.Where(cm => !cm.IsDeleted))
            .Include(c => c.Attachments)
            .Include(c => c.TimeEntries)
            .Include(c => c.SprintCards).ThenInclude(sc => sc.Sprint)
            .Where(c => c.SwimlaneId == swimlaneId && !c.IsDeleted);

        if (!includeArchived)
            query = query.Where(c => !c.IsArchived);

        var cards = await query
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);

        var allUserIds = cards.SelectMany(c => c.Assignments.Select(a => a.UserId)).Distinct().ToList();
        var displayNames = await ResolveDisplayNamesAsync(allUserIds, cancellationToken);
        var avatarUrls = await ResolveAvatarUrlsAsync(allUserIds, cancellationToken);

        return cards.Select(c => MapToDto(c, displayNames, avatarUrls)).ToList();
    }

    /// <summary>
    /// Updates a card. Requires Member role or higher.
    /// </summary>
    public async Task<CardDto> UpdateCardAsync(Guid cardId, UpdateCardDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var updatedFields = new List<string>();
        if (dto.Title is not null) { card.Title = dto.Title; updatedFields.Add("title"); }
        if (dto.Description is not null) { card.Description = dto.Description; updatedFields.Add("description"); }
        if (dto.Priority.HasValue) { card.Priority = dto.Priority.Value; updatedFields.Add("priority"); }
        if (dto.DueDate.HasValue)
        {
            card.DueDate = dto.DueDate.Value.Kind == DateTimeKind.Utc
                ? dto.DueDate.Value
                : DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc);
            updatedFields.Add("due date");
        }
        if (dto.StoryPoints.HasValue) { card.StoryPoints = dto.StoryPoints.Value; updatedFields.Add("story points"); }
        if (dto.IsArchived.HasValue) { card.IsArchived = dto.IsArchived.Value; updatedFields.Add(dto.IsArchived.Value ? "archived" : "unarchived"); }

        card.UpdatedAt = DateTime.UtcNow;
        card.ETag = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Card {CardId} updated by user {UserId}", cardId, caller.UserId);

        var updateDetails = updatedFields.Count > 0
            ? $"{{\"fields\":\"{string.Join(", ", updatedFields)}\"}}"
            : null;
        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "card.updated", "Card", cardId, updateDetails, cancellationToken);

        await _eventBus.PublishAsync(new CardUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = cardId,
            BoardId = card.Swimlane.BoardId,
            UpdatedByUserId = caller.UserId
        }, caller, cancellationToken);

        return await GetCardDtoAsync(cardId, cancellationToken)
            ?? throw new System.InvalidOperationException("Card was updated but could not be retrieved.");
    }

    /// <summary>
    /// Moves a card to a different swimlane and/or position. Enforces WIP limits.
    /// </summary>
    public async Task<CardDto> MoveCardAsync(Guid cardId, MoveCardDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var targetSwimlane = await _db.BoardSwimlanes
            .FirstOrDefaultAsync(l => l.Id == dto.TargetSwimlaneId && !l.IsArchived, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardSwimlaneNotFound, "Target swimlane not found.");

        // WIP limit check on target swimlane (skip if moving within same swimlane)
        if (card.SwimlaneId != dto.TargetSwimlaneId && targetSwimlane.CardLimit.HasValue)
        {
            var activeCount = await _db.Cards
                .CountAsync(c => c.SwimlaneId == dto.TargetSwimlaneId && !c.IsDeleted && !c.IsArchived, cancellationToken);

            if (activeCount >= targetSwimlane.CardLimit.Value)
                throw new ValidationException(ErrorCodes.WipLimitExceeded,
                    $"Target swimlane '{targetSwimlane.Title}' has reached its WIP limit of {targetSwimlane.CardLimit.Value}.");
        }

        var fromSwimlaneId = card.SwimlaneId;
        var fromSwimlaneTitle = card.Swimlane!.Title;
        card.SwimlaneId = dto.TargetSwimlaneId;
        card.Position = dto.Position;
        card.UpdatedAt = DateTime.UtcNow;
        card.ETag = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Card {CardId} moved from swimlane {FromSwimlaneId} to {ToSwimlaneId} by user {UserId}",
            cardId, fromSwimlaneId, dto.TargetSwimlaneId, caller.UserId);

        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "card.moved", "Card", cardId,
            $"{{\"from\":\"{fromSwimlaneId}\",\"to\":\"{dto.TargetSwimlaneId}\",\"fromTitle\":\"{fromSwimlaneTitle}\",\"toTitle\":\"{targetSwimlane.Title}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new CardMovedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = cardId,
            BoardId = card.Swimlane.BoardId,
            FromSwimlaneId = fromSwimlaneId,
            ToSwimlaneId = dto.TargetSwimlaneId,
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
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        card.IsDeleted = true;
        card.DeletedAt = DateTime.UtcNow;
        card.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Card {CardId} deleted by user {UserId}", cardId, caller.UserId);

        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "card.deleted", "Card", cardId,
            $"{{\"title\":\"{card.Title}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new CardDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = cardId,
            BoardId = card.Swimlane.BoardId,
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
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        // Target user must be a board member
        await _boardService.EnsureBoardMemberAsync(card.Swimlane.BoardId, userId, cancellationToken);

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

        var assignNames = await ResolveDisplayNamesAsync([userId], cancellationToken);
        var assignDisplayName = assignNames.TryGetValue(userId, out var an) ? an : userId.ToString()[..8];
        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "card.assigned", "Card", cardId,
            $"{{\"userId\":\"{userId}\",\"displayName\":\"{assignDisplayName}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new CardAssignedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = cardId,
            BoardId = card.Swimlane.BoardId,
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
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var assignment = await _db.CardAssignments
            .FirstOrDefaultAsync(a => a.CardId == cardId && a.UserId == userId, cancellationToken);

        if (assignment is null)
            return; // Idempotent

        _db.CardAssignments.Remove(assignment);
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var unassignNames = await ResolveDisplayNamesAsync([userId], cancellationToken);
        var unassignDisplayName = unassignNames.TryGetValue(userId, out var un) ? un : userId.ToString()[..8];
        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "card.unassigned", "Card", cardId,
            $"{{\"userId\":\"{userId}\",\"displayName\":\"{unassignDisplayName}\"}}", cancellationToken);
    }

    private async Task<CardDto?> GetCardDtoAsync(Guid cardId, CancellationToken cancellationToken)
    {
        var card = await _db.Cards
            .AsNoTracking()
            .Include(c => c.Swimlane)
            .Include(c => c.Assignments)
            .Include(c => c.CardLabels).ThenInclude(cl => cl.Label)
            .Include(c => c.Checklists).ThenInclude(ch => ch.Items)
            .Include(c => c.Comments.Where(cm => !cm.IsDeleted))
            .Include(c => c.Attachments)
            .Include(c => c.TimeEntries)
            .Include(c => c.SprintCards).ThenInclude(sc => sc.Sprint)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken);

        if (card is null) return null;

        var assigneeIds = card.Assignments.Select(a => a.UserId).ToList();
        var displayNames = await ResolveDisplayNamesAsync(assigneeIds, cancellationToken);
        var avatarUrls = await ResolveAvatarUrlsAsync(assigneeIds, cancellationToken);
        return MapToDto(card, displayNames, avatarUrls);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveDisplayNamesAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        if (_userDirectory is null || userIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await _userDirectory.GetDisplayNamesAsync(userIds, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveAvatarUrlsAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        if (_userDirectory is null || userIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await _userDirectory.GetAvatarUrlsAsync(userIds, cancellationToken);
    }

    internal static CardDto MapToDto(Card c, IReadOnlyDictionary<Guid, string>? displayNames = null, IReadOnlyDictionary<Guid, string>? avatarUrls = null) => new()
    {
        Id = c.Id,
        SwimlaneId = c.SwimlaneId,
        BoardId = c.Swimlane?.BoardId ?? Guid.Empty,
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
            DisplayName = displayNames is not null && displayNames.TryGetValue(a.UserId, out var name) ? name : null,
            AvatarUrl = avatarUrls is not null && avatarUrls.TryGetValue(a.UserId, out var url) ? url : null,
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
        TotalTrackedMinutes = c.TimeEntries.Sum(t => t.DurationMinutes),
        SprintId = c.SprintCards.FirstOrDefault(sc => sc.Sprint is not null &&
            sc.Sprint.Status != SprintStatus.Completed)?.SprintId,
        SprintTitle = c.SprintCards.FirstOrDefault(sc => sc.Sprint is not null &&
            sc.Sprint.Status != SprintStatus.Completed)?.Sprint?.Title,
        IsInDoneSwimlane = c.Swimlane?.IsDone ?? false
    };
}
