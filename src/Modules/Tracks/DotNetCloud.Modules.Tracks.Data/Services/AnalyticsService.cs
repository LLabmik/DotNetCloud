using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for board and team analytics — cycle time, workload distribution, and completion metrics.
/// </summary>
public sealed class AnalyticsService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ILogger<AnalyticsService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsService"/> class.
    /// </summary>
    public AnalyticsService(TracksDbContext db, BoardService boardService, ILogger<AnalyticsService> logger)
    {
        _db = db;
        _boardService = boardService;
        _logger = logger;
    }

    /// <summary>
    /// Gets analytics for a board: card counts, cycle time, per-swimlane and per-user distribution, completion trend.
    /// </summary>
    public async Task<BoardAnalyticsDto> GetBoardAnalyticsAsync(Guid boardId, CallerContext caller, int daysBack = 30, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        var boardExists = await _db.Boards.AnyAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken);
        if (!boardExists)
            throw new ValidationException(ErrorCodes.BoardNotFound, "Board not found.");

        // All cards (including archived for metrics)
        var allCards = await _db.Cards
            .AsNoTracking()
            .Include(c => c.Swimlane)
            .Include(c => c.Assignments)
            .Where(c => c.Swimlane!.BoardId == boardId && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var totalCards = allCards.Count;
        var completedCards = allCards.Count(c => c.IsArchived);
        var overdueCards = allCards.Count(c => !c.IsArchived && c.DueDate.HasValue && c.DueDate.Value < DateTime.UtcNow);

        // Average cycle time: creation → archive for completed cards
        var cycleTimeHours = 0.0;
        var archivedWithTime = allCards.Where(c => c.IsArchived && c.UpdatedAt > c.CreatedAt).ToList();
        if (archivedWithTime.Count > 0)
        {
            cycleTimeHours = archivedWithTime
                .Average(c => (c.UpdatedAt - c.CreatedAt).TotalHours);
        }

        // Cards per swimlane
        var swimlanes = await _db.BoardSwimlanes
            .AsNoTracking()
            .Where(l => l.BoardId == boardId && !l.IsArchived)
            .OrderBy(l => l.Position)
            .ToListAsync(cancellationToken);

        var cardsByList = new Dictionary<string, int>();
        foreach (var swimlane in swimlanes)
        {
            var count = allCards.Count(c => c.SwimlaneId == swimlane.Id && !c.IsArchived);
            cardsByList[swimlane.Title] = count;
        }

        // Cards per assignee
        var cardsByAssignee = allCards
            .Where(c => !c.IsArchived)
            .SelectMany(c => c.Assignments.Select(a => a.UserId))
            .GroupBy(uid => uid)
            .ToDictionary(g => g.Key, g => g.Count());

        // Completion trend (cards archived per day)
        var cutoff = DateTime.UtcNow.AddDays(-daysBack);
        var completedRecently = allCards
            .Where(c => c.IsArchived && c.UpdatedAt >= cutoff)
            .ToList();

        var completionsByDay = new List<DailyCompletionDto>();
        for (var d = 0; d < daysBack; d++)
        {
            var date = DateOnly.FromDateTime(cutoff.AddDays(d));
            var count = completedRecently.Count(c =>
                DateOnly.FromDateTime(c.UpdatedAt) == date);
            completionsByDay.Add(new DailyCompletionDto { Date = date, Count = count });
        }

        return new BoardAnalyticsDto
        {
            BoardId = boardId,
            TotalCards = totalCards,
            CompletedCards = completedCards,
            OverdueCards = overdueCards,
            AverageCycleTimeHours = Math.Round(cycleTimeHours, 2),
            CardsByList = cardsByList,
            CardsByAssignee = cardsByAssignee,
            CompletionsOverTime = completionsByDay
        };
    }

    /// <summary>
    /// Gets team-level analytics aggregated across all team boards.
    /// </summary>
    public async Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid teamId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Verify user has a team role
        var hasRole = await _db.TeamRoles.AnyAsync(r => r.CoreTeamId == teamId && r.UserId == caller.UserId, cancellationToken);
        if (!hasRole)
            throw new ValidationException(ErrorCodes.TracksNotTeamMember, "Not a member of this team.");

        var teamBoards = await _db.Boards
            .AsNoTracking()
            .Where(b => b.TeamId == teamId && !b.IsDeleted)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        var allCards = await _db.Cards
            .AsNoTracking()
            .Include(c => c.Swimlane)
            .Include(c => c.Assignments)
            .Where(c => teamBoards.Contains(c.Swimlane!.BoardId) && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var cardsByMember = allCards
            .Where(c => !c.IsArchived)
            .SelectMany(c => c.Assignments.Select(a => a.UserId))
            .GroupBy(uid => uid)
            .ToDictionary(g => g.Key, g => g.Count());

        return new TeamAnalyticsDto
        {
            TeamId = teamId,
            BoardCount = teamBoards.Count,
            TotalCards = allCards.Count,
            CompletedCards = allCards.Count(c => c.IsArchived),
            CardsByMember = cardsByMember
        };
    }
}
