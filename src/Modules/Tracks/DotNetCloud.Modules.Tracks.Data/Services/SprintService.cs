using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing sprints on boards.
/// </summary>
public sealed class SprintService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<SprintService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SprintService"/> class.
    /// </summary>
    public SprintService(TracksDbContext db, BoardService boardService, ActivityService activityService, IEventBus eventBus, ILogger<SprintService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new sprint on a board. Requires Admin role or higher.
    /// </summary>
    public async Task<SprintDto> CreateSprintAsync(Guid boardId, CreateSprintDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await _boardService.EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        var boardExists = await _db.Boards.AnyAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken);
        if (!boardExists)
            throw new ValidationException(ErrorCodes.BoardNotFound, "Board not found.");

        var sprint = new Sprint
        {
            BoardId = boardId,
            Title = dto.Title,
            Goal = dto.Goal,
            StartDate = dto.StartDate.HasValue ? DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Utc) : null,
            EndDate = dto.EndDate.HasValue ? DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Utc) : null,
            TargetStoryPoints = dto.TargetStoryPoints,
            Status = SprintStatus.Planning
        };

        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sprint {SprintId} '{Title}' created on board {BoardId} by user {UserId}",
            sprint.Id, sprint.Title, boardId, caller.UserId);

        await _activityService.LogAsync(boardId, caller.UserId, "sprint.created", "Sprint", sprint.Id,
            $"{{\"title\":\"{sprint.Title}\"}}", cancellationToken);

        return MapToDto(sprint);
    }

    /// <summary>
    /// Gets all sprints for a board.
    /// </summary>
    public async Task<IReadOnlyList<SprintDto>> GetSprintsAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        var sprints = await _db.Sprints
            .AsNoTracking()
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card)
            .Where(s => s.BoardId == boardId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sprints.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets a sprint by ID.
    /// </summary>
    public async Task<SprintDto?> GetSprintAsync(Guid sprintId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var sprint = await _db.Sprints
            .AsNoTracking()
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card)
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken);

        if (sprint is null)
            return null;

        await _boardService.EnsureBoardMemberAsync(sprint.BoardId, caller.UserId, cancellationToken);

        return MapToDto(sprint);
    }

    /// <summary>
    /// Updates a sprint. Requires Admin role or higher.
    /// </summary>
    public async Task<SprintDto> UpdateSprintAsync(Guid sprintId, UpdateSprintDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var sprint = await _db.Sprints
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card)
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.SprintNotFound, "Sprint not found.");

        await _boardService.EnsureBoardRoleAsync(sprint.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        if (dto.Title is not null) sprint.Title = dto.Title;
        if (dto.Goal is not null) sprint.Goal = dto.Goal;
        if (dto.StartDate.HasValue) sprint.StartDate = DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Utc);
        if (dto.EndDate.HasValue) sprint.EndDate = DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Utc);
        if (dto.TargetStoryPoints.HasValue) sprint.TargetStoryPoints = dto.TargetStoryPoints;

        sprint.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sprint {SprintId} updated by user {UserId}", sprintId, caller.UserId);

        return MapToDto(sprint);
    }

    /// <summary>
    /// Starts a sprint. Only one sprint per board can be active. Requires Admin role.
    /// </summary>
    public async Task<SprintDto> StartSprintAsync(Guid sprintId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var sprint = await _db.Sprints
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card)
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.SprintNotFound, "Sprint not found.");

        await _boardService.EnsureBoardRoleAsync(sprint.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        if (sprint.Status != SprintStatus.Planning)
            throw new ValidationException(ErrorCodes.InvalidSprintTransition,
                $"Cannot start a sprint in {sprint.Status} status. Only Planning sprints can be started.");

        // Check for existing active sprint on this board
        var hasActive = await _db.Sprints
            .AnyAsync(s => s.BoardId == sprint.BoardId && s.Status == SprintStatus.Active && s.Id != sprintId, cancellationToken);

        if (hasActive)
            throw new ValidationException(ErrorCodes.ActiveSprintExists,
                "This board already has an active sprint. Complete it before starting a new one.");

        sprint.Status = SprintStatus.Active;
        sprint.StartDate ??= DateTime.UtcNow;
        sprint.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sprint {SprintId} started by user {UserId}", sprintId, caller.UserId);

        await _activityService.LogAsync(sprint.BoardId, caller.UserId, "sprint.started", "Sprint", sprintId,
            $"{{\"title\":\"{sprint.Title}\"}}", cancellationToken);

        await _eventBus.PublishAsync(new SprintStartedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SprintId = sprintId,
            BoardId = sprint.BoardId,
            Title = sprint.Title,
            StartedByUserId = caller.UserId
        }, caller, cancellationToken);

        return MapToDto(sprint);
    }

    /// <summary>
    /// Completes a sprint. Requires Admin role.
    /// </summary>
    public async Task<SprintDto> CompleteSprintAsync(Guid sprintId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var sprint = await _db.Sprints
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card)
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.SprintNotFound, "Sprint not found.");

        await _boardService.EnsureBoardRoleAsync(sprint.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        if (sprint.Status != SprintStatus.Active)
            throw new ValidationException(ErrorCodes.InvalidSprintTransition,
                $"Cannot complete a sprint in {sprint.Status} status. Only Active sprints can be completed.");

        var cards = sprint.SprintCards.Select(sc => sc.Card).Where(c => c is not null).ToList();
        var completedCount = cards.Count(c => c!.IsArchived);

        sprint.Status = SprintStatus.Completed;
        sprint.EndDate ??= DateTime.UtcNow;
        sprint.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sprint {SprintId} completed by user {UserId}", sprintId, caller.UserId);

        await _activityService.LogAsync(sprint.BoardId, caller.UserId, "sprint.completed", "Sprint", sprintId,
            $"{{\"title\":\"{sprint.Title}\",\"completed\":{completedCount},\"total\":{cards.Count}}}", cancellationToken);

        await _eventBus.PublishAsync(new SprintCompletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SprintId = sprintId,
            BoardId = sprint.BoardId,
            Title = sprint.Title,
            CompletedByUserId = caller.UserId,
            CompletedCardCount = completedCount,
            TotalCardCount = cards.Count
        }, caller, cancellationToken);

        return MapToDto(sprint);
    }

    /// <summary>
    /// Deletes a sprint. Only Planning sprints can be deleted. Requires Admin role.
    /// </summary>
    public async Task DeleteSprintAsync(Guid sprintId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var sprint = await _db.Sprints
            .Include(s => s.SprintCards)
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.SprintNotFound, "Sprint not found.");

        await _boardService.EnsureBoardRoleAsync(sprint.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        // Remove sprint-card associations
        _db.SprintCards.RemoveRange(sprint.SprintCards);
        _db.Sprints.Remove(sprint);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sprint {SprintId} deleted by user {UserId}", sprintId, caller.UserId);
    }

    /// <summary>
    /// Adds a card to a sprint. Requires Member role or higher.
    /// </summary>
    public async Task AddCardToSprintAsync(Guid sprintId, Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var sprint = await _db.Sprints
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.SprintNotFound, "Sprint not found.");

        await _boardService.EnsureBoardRoleAsync(sprint.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var cardExists = await _db.Cards.AnyAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken);
        if (!cardExists)
            throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        var alreadyInSprint = await _db.SprintCards
            .AnyAsync(sc => sc.SprintId == sprintId && sc.CardId == cardId, cancellationToken);

        if (alreadyInSprint)
            return; // Idempotent

        _db.SprintCards.Add(new SprintCard
        {
            SprintId = sprintId,
            CardId = cardId,
            AddedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Removes a card from a sprint. Requires Member role or higher.
    /// </summary>
    public async Task RemoveCardFromSprintAsync(Guid sprintId, Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var sprint = await _db.Sprints
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.SprintNotFound, "Sprint not found.");

        await _boardService.EnsureBoardRoleAsync(sprint.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var sprintCard = await _db.SprintCards
            .FirstOrDefaultAsync(sc => sc.SprintId == sprintId && sc.CardId == cardId, cancellationToken);

        if (sprintCard is null)
            return; // Idempotent

        _db.SprintCards.Remove(sprintCard);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all cards in a sprint with full details.
    /// </summary>
    public async Task<IReadOnlyList<CardDto>> GetSprintCardsAsync(Guid sprintId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var sprint = await _db.Sprints
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card).ThenInclude(c => c!.Swimlane)
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card).ThenInclude(c => c!.Assignments)
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card).ThenInclude(c => c!.CardLabels).ThenInclude(cl => cl.Label)
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card).ThenInclude(c => c!.Checklists).ThenInclude(ch => ch.Items)
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card).ThenInclude(c => c!.Comments)
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card).ThenInclude(c => c!.Attachments)
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card).ThenInclude(c => c!.TimeEntries)
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.SprintNotFound, "Sprint not found.");

        await _boardService.EnsureBoardMemberAsync(sprint.BoardId, caller.UserId, cancellationToken);

        return sprint.SprintCards
            .Where(sc => sc.Card is not null && !sc.Card.IsDeleted)
            .GroupBy(sc => sc.CardId)
            .Select(g => g.First().Card!)
            .Select(c => CardService.MapToDto(c!) with
            {
                SprintId = sprint.Id,
                SprintTitle = sprint.Title
            })
            .ToList();
    }

    /// <summary>
    /// Gets all cards on a board that are not assigned to any active or planning sprint.
    /// </summary>
    public async Task<IReadOnlyList<CardDto>> GetBacklogCardsAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        // Find sprint IDs that are active or planning
        var activeSprintIds = await _db.Sprints
            .Where(s => s.BoardId == boardId && (s.Status == SprintStatus.Planning || s.Status == SprintStatus.Active))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        // Get card IDs assigned to any of those sprints
        var assignedCardIds = await _db.SprintCards
            .Where(sc => activeSprintIds.Contains(sc.SprintId))
            .Select(sc => sc.CardId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Get all swimlanes for the board
        var swimlaneIds = await _db.BoardSwimlanes
            .Where(l => l.BoardId == boardId)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        // Get all cards not in any active/planning sprint
        var cards = await _db.Cards
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(c => c.Swimlane)
            .Include(c => c.Assignments)
            .Include(c => c.CardLabels).ThenInclude(cl => cl.Label)
            .Include(c => c.Checklists).ThenInclude(ch => ch.Items)
            .Include(c => c.Comments)
            .Include(c => c.Attachments)
            .Include(c => c.TimeEntries)
            .Include(c => c.SprintCards).ThenInclude(sc => sc.Sprint)
            .Where(c => swimlaneIds.Contains(c.SwimlaneId) && !c.IsDeleted && !c.IsArchived
                        && !assignedCardIds.Contains(c.Id))
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Position)
            .ToListAsync(cancellationToken);

        return cards.Select(c => CardService.MapToDto(c)).ToList();
    }

    /// <summary>
    /// Batch-adds multiple cards to a sprint.
    /// </summary>
    public async Task BatchAddCardsAsync(Guid sprintId, IReadOnlyList<Guid> cardIds, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var sprint = await _db.Sprints
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.SprintNotFound, "Sprint not found.");

        await _boardService.EnsureBoardRoleAsync(sprint.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var existingCardIds = await _db.SprintCards
            .Where(sc => sc.SprintId == sprintId)
            .Select(sc => sc.CardId)
            .ToListAsync(cancellationToken);

        var validCardIds = await _db.Cards
            .Where(c => cardIds.Contains(c.Id) && !c.IsDeleted)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        foreach (var cardId in validCardIds.Where(id => !existingCardIds.Contains(id)))
        {
            _db.SprintCards.Add(new SprintCard
            {
                SprintId = sprintId,
                CardId = cardId,
                AddedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static SprintDto MapToDto(Sprint s)
    {
        var cards = s.SprintCards
            .Where(sc => sc.Card is not null)
            .Select(sc => sc.Card!)
            .ToList();

        return new SprintDto
        {
            Id = s.Id,
            BoardId = s.BoardId,
            Title = s.Title,
            Goal = s.Goal,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            Status = s.Status,
            CardCount = cards.Count,
            TotalStoryPoints = cards.Where(c => c.StoryPoints.HasValue).Sum(c => c.StoryPoints!.Value),
            CompletedStoryPoints = cards.Where(c => c.IsArchived && c.StoryPoints.HasValue).Sum(c => c.StoryPoints!.Value),
            TargetStoryPoints = s.TargetStoryPoints,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        };
    }
}
