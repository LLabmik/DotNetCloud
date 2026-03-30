using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing board labels.
/// </summary>
public sealed class LabelService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly ILogger<LabelService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LabelService"/> class.
    /// </summary>
    public LabelService(TracksDbContext db, BoardService boardService, ActivityService activityService, ILogger<LabelService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new label on a board. Requires Admin role or higher.
    /// </summary>
    public async Task<LabelDto> CreateLabelAsync(Guid boardId, CreateLabelDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await _boardService.EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        var boardExists = await _db.Boards.AnyAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken);
        if (!boardExists)
            throw new ValidationException(ErrorCodes.BoardNotFound, "Board not found.");

        var label = new Label
        {
            BoardId = boardId,
            Title = dto.Title,
            Color = dto.Color
        };

        _db.Labels.Add(label);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Label {LabelId} '{Title}' created on board {BoardId} by user {UserId}",
            label.Id, label.Title, boardId, caller.UserId);

        await _activityService.LogAsync(boardId, caller.UserId, "label.created", "Label", label.Id,
            $"{{\"title\":\"{label.Title}\",\"color\":\"{label.Color}\"}}", cancellationToken);

        return MapToDto(label);
    }

    /// <summary>
    /// Gets all labels for a board.
    /// </summary>
    public async Task<IReadOnlyList<LabelDto>> GetLabelsAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        var labels = await _db.Labels
            .AsNoTracking()
            .Where(l => l.BoardId == boardId)
            .OrderBy(l => l.Title)
            .ToListAsync(cancellationToken);

        return labels.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Updates a label. Requires Admin role or higher.
    /// </summary>
    public async Task<LabelDto> UpdateLabelAsync(Guid labelId, UpdateLabelDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var label = await _db.Labels
            .FirstOrDefaultAsync(l => l.Id == labelId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.LabelNotFound, "Label not found.");

        await _boardService.EnsureBoardRoleAsync(label.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        if (dto.Title is not null) label.Title = dto.Title;
        if (dto.Color is not null) label.Color = dto.Color;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Label {LabelId} updated by user {UserId}", labelId, caller.UserId);

        return MapToDto(label);
    }

    /// <summary>
    /// Deletes a label and removes it from all cards. Requires Admin role or higher.
    /// </summary>
    public async Task DeleteLabelAsync(Guid labelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var label = await _db.Labels
            .FirstOrDefaultAsync(l => l.Id == labelId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.LabelNotFound, "Label not found.");

        await _boardService.EnsureBoardRoleAsync(label.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);

        // Remove all card-label associations
        var cardLabels = await _db.CardLabels
            .Where(cl => cl.LabelId == labelId)
            .ToListAsync(cancellationToken);

        _db.CardLabels.RemoveRange(cardLabels);
        _db.Labels.Remove(label);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Label {LabelId} deleted by user {UserId}", labelId, caller.UserId);

        await _activityService.LogAsync(label.BoardId, caller.UserId, "label.deleted", "Label", labelId,
            $"{{\"title\":\"{label.Title}\"}}", cancellationToken);
    }

    /// <summary>
    /// Adds a label to a card. Requires Member role or higher.
    /// </summary>
    public async Task AddLabelToCardAsync(Guid cardId, Guid labelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var labelExists = await _db.Labels
            .AnyAsync(l => l.Id == labelId && l.BoardId == card.Swimlane.BoardId, cancellationToken);

        if (!labelExists)
            throw new ValidationException(ErrorCodes.LabelNotFound, "Label not found on this board.");

        var alreadyApplied = await _db.CardLabels
            .AnyAsync(cl => cl.CardId == cardId && cl.LabelId == labelId, cancellationToken);

        if (alreadyApplied)
            return; // Idempotent

        _db.CardLabels.Add(new CardLabel
        {
            CardId = cardId,
            LabelId = labelId,
            AppliedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Removes a label from a card. Requires Member role or higher.
    /// </summary>
    public async Task RemoveLabelFromCardAsync(Guid cardId, Guid labelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var cardLabel = await _db.CardLabels
            .FirstOrDefaultAsync(cl => cl.CardId == cardId && cl.LabelId == labelId, cancellationToken);

        if (cardLabel is null)
            return; // Idempotent

        _db.CardLabels.Remove(cardLabel);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static LabelDto MapToDto(Label l) => new()
    {
        Id = l.Id,
        BoardId = l.BoardId,
        Title = l.Title,
        Color = l.Color
    };
}
