using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing card checklists and checklist items.
/// </summary>
public sealed class ChecklistService
{
    private const double PositionGap = 1000.0;

    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly ILogger<ChecklistService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChecklistService"/> class.
    /// </summary>
    public ChecklistService(TracksDbContext db, BoardService boardService, ActivityService activityService, ILogger<ChecklistService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new checklist on a card. Requires Member role or higher.
    /// </summary>
    public async Task<CardChecklistDto> CreateChecklistAsync(Guid cardId, string title, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var card = await _db.Cards
            .Include(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var maxPos = await _db.CardChecklists
            .Where(ch => ch.CardId == cardId)
            .MaxAsync(ch => (double?)ch.Position, cancellationToken) ?? 0;

        var checklist = new CardChecklist
        {
            CardId = cardId,
            Title = title,
            Position = maxPos + PositionGap
        };

        _db.CardChecklists.Add(checklist);
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Checklist {ChecklistId} '{Title}' created on card {CardId} by user {UserId}",
            checklist.Id, title, cardId, caller.UserId);

        await _activityService.LogAsync(card.List.BoardId, caller.UserId, "checklist.created", "CardChecklist", checklist.Id,
            $"{{\"title\":\"{title}\",\"cardId\":\"{cardId}\"}}", cancellationToken);

        return MapToDto(checklist);
    }

    /// <summary>
    /// Gets all checklists for a card with items.
    /// </summary>
    public async Task<IReadOnlyList<CardChecklistDto>> GetChecklistsAsync(Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .AsNoTracking()
            .Include(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardMemberAsync(card.List!.BoardId, caller.UserId, cancellationToken);

        var checklists = await _db.CardChecklists
            .AsNoTracking()
            .Include(ch => ch.Items)
            .Where(ch => ch.CardId == cardId)
            .OrderBy(ch => ch.Position)
            .ToListAsync(cancellationToken);

        return checklists.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Deletes a checklist and all its items. Requires Member role or higher.
    /// </summary>
    public async Task DeleteChecklistAsync(Guid checklistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var checklist = await _db.CardChecklists
            .Include(ch => ch.Items)
            .Include(ch => ch.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(ch => ch.Id == checklistId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.ChecklistNotFound, "Checklist not found.");

        await _boardService.EnsureBoardRoleAsync(checklist.Card!.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        _db.ChecklistItems.RemoveRange(checklist.Items);
        _db.CardChecklists.Remove(checklist);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Checklist {ChecklistId} deleted by user {UserId}", checklistId, caller.UserId);
    }

    /// <summary>
    /// Adds an item to a checklist. Requires Member role or higher.
    /// </summary>
    public async Task<ChecklistItemDto> AddItemAsync(Guid checklistId, string title, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var checklist = await _db.CardChecklists
            .Include(ch => ch.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(ch => ch.Id == checklistId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.ChecklistNotFound, "Checklist not found.");

        await _boardService.EnsureBoardRoleAsync(checklist.Card!.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var maxPos = await _db.ChecklistItems
            .Where(i => i.ChecklistId == checklistId)
            .MaxAsync(i => (double?)i.Position, cancellationToken) ?? 0;

        var item = new ChecklistItem
        {
            ChecklistId = checklistId,
            Title = title,
            Position = maxPos + PositionGap
        };

        _db.ChecklistItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return MapItemToDto(item);
    }

    /// <summary>
    /// Toggles a checklist item's completion status. Requires Member role or higher.
    /// </summary>
    public async Task<ChecklistItemDto> ToggleItemAsync(Guid itemId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var item = await _db.ChecklistItems
            .Include(i => i.Checklist).ThenInclude(ch => ch!.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.ChecklistNotFound, "Checklist item not found.");

        await _boardService.EnsureBoardRoleAsync(item.Checklist!.Card!.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        item.IsCompleted = !item.IsCompleted;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return MapItemToDto(item);
    }

    /// <summary>
    /// Deletes a checklist item. Requires Member role or higher.
    /// </summary>
    public async Task DeleteItemAsync(Guid itemId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var item = await _db.ChecklistItems
            .Include(i => i.Checklist).ThenInclude(ch => ch!.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.ChecklistNotFound, "Checklist item not found.");

        await _boardService.EnsureBoardRoleAsync(item.Checklist!.Card!.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        _db.ChecklistItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static CardChecklistDto MapToDto(CardChecklist ch) => new()
    {
        Id = ch.Id,
        CardId = ch.CardId,
        Title = ch.Title,
        Position = (int)ch.Position,
        Items = ch.Items.OrderBy(i => i.Position).Select(MapItemToDto).ToList()
    };

    private static ChecklistItemDto MapItemToDto(ChecklistItem i) => new()
    {
        Id = i.Id,
        Title = i.Title,
        IsCompleted = i.IsCompleted,
        Position = (int)i.Position
    };
}
