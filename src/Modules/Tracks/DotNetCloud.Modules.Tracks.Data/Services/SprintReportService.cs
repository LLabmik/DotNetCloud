using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for sprint reporting — velocity, burndown, and completion metrics.
/// </summary>
public sealed class SprintReportService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ILogger<SprintReportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SprintReportService"/> class.
    /// </summary>
    public SprintReportService(TracksDbContext db, BoardService boardService, ILogger<SprintReportService> logger)
    {
        _db = db;
        _boardService = boardService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a sprint report with burndown data and completion metrics.
    /// </summary>
    public async Task<SprintReportDto> GetSprintReportAsync(Guid sprintId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var sprint = await _db.Sprints
            .AsNoTracking()
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card)
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.SprintNotFound, "Sprint not found.");

        await _boardService.EnsureBoardMemberAsync(sprint.BoardId, caller.UserId, cancellationToken);

        var cards = sprint.SprintCards
            .Select(sc => sc.Card)
            .Where(c => c is not null && !c.IsDeleted)
            .ToList();

        var totalCards = cards.Count;
        var completedCards = cards.Count(c => c!.IsArchived);
        var totalPoints = cards.Sum(c => c!.StoryPoints ?? 0);
        var completedPoints = cards.Where(c => c!.IsArchived).Sum(c => c!.StoryPoints ?? 0);

        var burndown = BuildBurndownData(sprint, cards!);

        return new SprintReportDto
        {
            SprintId = sprintId,
            Title = sprint.Title,
            Status = sprint.Status,
            TotalCards = totalCards,
            CompletedCards = completedCards,
            TotalPoints = totalPoints,
            CompletedPoints = completedPoints,
            BurndownData = burndown
        };
    }

    /// <summary>
    /// Gets velocity across all completed sprints on a board, ordered chronologically.
    /// </summary>
    public async Task<IReadOnlyList<SprintVelocityDto>> GetBoardVelocityAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        var sprints = await _db.Sprints
            .AsNoTracking()
            .Include(s => s.SprintCards).ThenInclude(sc => sc.Card)
            .Where(s => s.BoardId == boardId && s.Status == SprintStatus.Completed)
            .OrderBy(s => s.EndDate)
            .ToListAsync(cancellationToken);

        return sprints.Select(s =>
        {
            var cards = s.SprintCards.Select(sc => sc.Card).Where(c => c is not null && !c.IsDeleted).ToList();
            return new SprintVelocityDto
            {
                SprintId = s.Id,
                Title = s.Title,
                CommittedCards = cards.Count,
                CommittedPoints = cards.Sum(c => c!.StoryPoints ?? 0),
                CompletedCards = cards.Count(c => c!.IsArchived),
                CompletedPoints = cards.Where(c => c!.IsArchived).Sum(c => c!.StoryPoints ?? 0)
            };
        }).ToList();
    }

    private static IReadOnlyList<BurndownPointDto> BuildBurndownData(Sprint sprint, IReadOnlyList<Card> cards)
    {
        // Need both a start and end date to produce meaningful burndown
        var startDate = sprint.StartDate ?? sprint.CreatedAt;
        var endDate = sprint.EndDate ?? DateTime.UtcNow;

        if (endDate <= startDate)
            return [];

        var totalDays = (int)(endDate - startDate).TotalDays + 1;
        var totalPoints = cards.Sum(c => c.StoryPoints ?? 0);

        var result = new List<BurndownPointDto>(totalDays);

        for (var day = 0; day < totalDays; day++)
        {
            var date = DateOnly.FromDateTime(startDate.AddDays(day));

            // Remaining points = total - points on cards archived on or before this date
            var completedByDate = cards
                .Where(c => c.IsArchived && DateOnly.FromDateTime(c.UpdatedAt) <= date)
                .Sum(c => c.StoryPoints ?? 0);

            var remaining = Math.Max(0, totalPoints - completedByDate);

            // Ideal burndown: linear from totalPoints to 0
            var ideal = totalDays > 1
                ? (int)Math.Round(totalPoints * (1.0 - (double)day / (totalDays - 1)))
                : 0;

            result.Add(new BurndownPointDto
            {
                Date = date,
                RemainingPoints = remaining,
                IdealPoints = ideal
            });
        }

        return result;
    }
}
