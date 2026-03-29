using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for bulk card operations — move, assign, label, and archive multiple cards at once.
/// </summary>
public sealed class BulkOperationService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly ILogger<BulkOperationService> _logger;

    private const int MaxBulkSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkOperationService"/> class.
    /// </summary>
    public BulkOperationService(TracksDbContext db, BoardService boardService, ActivityService activityService, ILogger<BulkOperationService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Moves multiple cards to a target list. Cards that belong to different boards from the target list are skipped.
    /// </summary>
    public async Task<BulkOperationResultDto> BulkMoveCardsAsync(BulkMoveCardsDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.CardIds.Count == 0)
            throw new ValidationException(ErrorCodes.BulkOperationEmpty, "No card IDs provided.");

        var targetList = await _db.BoardLists
            .FirstOrDefaultAsync(l => l.Id == dto.TargetListId && !l.IsArchived, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardListNotFound, "Target list not found or archived.");

        await _boardService.EnsureBoardRoleAsync(targetList.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var normalizedIds = dto.CardIds.Take(MaxBulkSize).ToList();
        var cards = await _db.Cards
            .Include(c => c.List)
            .Where(c => normalizedIds.Contains(c.Id) && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var successes = 0;
        var failures = new Dictionary<Guid, string>();

        // Calculate starting position for moved cards (append to end of target list)
        var maxPos = await _db.Cards
            .Where(c => c.ListId == dto.TargetListId && !c.IsDeleted)
            .MaxAsync(c => (double?)c.Position, cancellationToken) ?? 0;

        var posStep = 1000.0;

        foreach (var cardId in normalizedIds)
        {
            var card = cards.FirstOrDefault(c => c.Id == cardId);
            if (card is null)
            {
                failures[cardId] = "Card not found.";
                continue;
            }

            // Card must be on the same board as the target list
            if (card.List?.BoardId != targetList.BoardId)
            {
                failures[cardId] = "Card belongs to a different board.";
                continue;
            }

            card.ListId = dto.TargetListId;
            maxPos += posStep;
            card.Position = maxPos;
            successes++;
        }

        if (successes > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            await _activityService.LogAsync(targetList.BoardId, caller.UserId, "bulk.move",
                "Card", Guid.Empty, $"{{\"count\":{successes},\"targetListId\":\"{dto.TargetListId}\"}}",
                cancellationToken);

            _logger.LogInformation("Bulk moved {Count} cards to list {ListId} by user {UserId}",
                successes, dto.TargetListId, caller.UserId);
        }

        return new BulkOperationResultDto
        {
            SuccessCount = successes,
            FailedCount = failures.Count,
            Failures = failures
        };
    }

    /// <summary>
    /// Assigns multiple cards to a user. Only cards the caller has Member access to are processed.
    /// </summary>
    public async Task<BulkOperationResultDto> BulkAssignCardsAsync(BulkAssignCardsDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.CardIds.Count == 0)
            throw new ValidationException(ErrorCodes.BulkOperationEmpty, "No card IDs provided.");

        var normalizedIds = dto.CardIds.Take(MaxBulkSize).ToList();
        var cards = await _db.Cards
            .Include(c => c.List)
            .Include(c => c.Assignments)
            .Where(c => normalizedIds.Contains(c.Id) && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        // Verify board membership for all cards' boards
        var boardIds = cards.Select(c => c.List?.BoardId).Where(id => id.HasValue).Select(id => id!.Value).Distinct();
        var accessibleBoardIds = new HashSet<Guid>();
        foreach (var boardId in boardIds)
        {
            try
            {
                await _boardService.EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Member, cancellationToken);
                accessibleBoardIds.Add(boardId);
            }
            catch (ValidationException)
            {
                // Skip boards caller doesn't have access to
            }
        }

        var successes = 0;
        var failures = new Dictionary<Guid, string>();

        foreach (var cardId in normalizedIds)
        {
            var card = cards.FirstOrDefault(c => c.Id == cardId);
            if (card is null)
            {
                failures[cardId] = "Card not found.";
                continue;
            }

            if (card.List?.BoardId is not { } boardId || !accessibleBoardIds.Contains(boardId))
            {
                failures[cardId] = "Insufficient board access.";
                continue;
            }

            // Skip if already assigned
            if (card.Assignments.Any(a => a.UserId == dto.UserId))
            {
                successes++;
                continue;
            }

            _db.CardAssignments.Add(new CardAssignment
            {
                CardId = cardId,
                UserId = dto.UserId,
                AssignedAt = DateTime.UtcNow
            });
            successes++;
        }

        if (successes > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Bulk assigned {Count} cards to user {AssigneeId} by user {UserId}",
                successes, dto.UserId, caller.UserId);
        }

        return new BulkOperationResultDto
        {
            SuccessCount = successes,
            FailedCount = failures.Count,
            Failures = failures
        };
    }

    /// <summary>
    /// Applies a label to multiple cards on the same board.
    /// </summary>
    public async Task<BulkOperationResultDto> BulkLabelCardsAsync(BulkLabelCardsDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.CardIds.Count == 0)
            throw new ValidationException(ErrorCodes.BulkOperationEmpty, "No card IDs provided.");

        var label = await _db.Labels
            .FirstOrDefaultAsync(l => l.Id == dto.LabelId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.LabelNotFound, "Label not found.");

        await _boardService.EnsureBoardRoleAsync(label.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var normalizedIds = dto.CardIds.Take(MaxBulkSize).ToList();
        var cards = await _db.Cards
            .Include(c => c.List)
            .Include(c => c.CardLabels)
            .Where(c => normalizedIds.Contains(c.Id) && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var successes = 0;
        var failures = new Dictionary<Guid, string>();

        foreach (var cardId in normalizedIds)
        {
            var card = cards.FirstOrDefault(c => c.Id == cardId);
            if (card is null)
            {
                failures[cardId] = "Card not found.";
                continue;
            }

            if (card.List?.BoardId != label.BoardId)
            {
                failures[cardId] = "Card is on a different board from the label.";
                continue;
            }

            // Skip if already labelled
            if (card.CardLabels.Any(cl => cl.LabelId == dto.LabelId))
            {
                successes++;
                continue;
            }

            _db.CardLabels.Add(new CardLabel
            {
                CardId = cardId,
                LabelId = dto.LabelId,
                AppliedAt = DateTime.UtcNow
            });
            successes++;
        }

        if (successes > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Bulk labelled {Count} cards with label {LabelId} by user {UserId}",
                successes, dto.LabelId, caller.UserId);
        }

        return new BulkOperationResultDto
        {
            SuccessCount = successes,
            FailedCount = failures.Count,
            Failures = failures
        };
    }

    /// <summary>
    /// Archives multiple cards at once.
    /// </summary>
    public async Task<BulkOperationResultDto> BulkArchiveCardsAsync(BulkCardOperationDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.CardIds.Count == 0)
            throw new ValidationException(ErrorCodes.BulkOperationEmpty, "No card IDs provided.");

        var normalizedIds = dto.CardIds.Take(MaxBulkSize).ToList();
        var cards = await _db.Cards
            .Include(c => c.List)
            .Where(c => normalizedIds.Contains(c.Id) && !c.IsDeleted && !c.IsArchived)
            .ToListAsync(cancellationToken);

        var boardIds = cards.Select(c => c.List?.BoardId).Where(id => id.HasValue).Select(id => id!.Value).Distinct();
        var accessibleBoardIds = new HashSet<Guid>();
        foreach (var boardId in boardIds)
        {
            try
            {
                await _boardService.EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Member, cancellationToken);
                accessibleBoardIds.Add(boardId);
            }
            catch (ValidationException) { }
        }

        var successes = 0;
        var failures = new Dictionary<Guid, string>();

        foreach (var cardId in normalizedIds)
        {
            var card = cards.FirstOrDefault(c => c.Id == cardId);
            if (card is null)
            {
                failures[cardId] = "Card not found or already archived.";
                continue;
            }

            if (card.List?.BoardId is not { } boardId || !accessibleBoardIds.Contains(boardId))
            {
                failures[cardId] = "Insufficient board access.";
                continue;
            }

            card.IsArchived = true;
            successes++;
        }

        if (successes > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Bulk archived {Count} cards by user {UserId}", successes, caller.UserId);
        }

        return new BulkOperationResultDto
        {
            SuccessCount = successes,
            FailedCount = failures.Count,
            Failures = failures
        };
    }
}
