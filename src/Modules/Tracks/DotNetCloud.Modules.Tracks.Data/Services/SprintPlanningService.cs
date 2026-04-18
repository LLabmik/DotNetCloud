using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing sprint year plans — bulk creation, duration adjustment, and plan overview.
/// Only available on Team-mode boards.
/// </summary>
public sealed class SprintPlanningService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly ILogger<SprintPlanningService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SprintPlanningService"/> class.
    /// </summary>
    public SprintPlanningService(TracksDbContext db, BoardService boardService, ActivityService activityService, ILogger<SprintPlanningService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a year plan of sequential sprints with equal default durations.
    /// Requires Admin role and Team-mode board.
    /// </summary>
    public async Task<SprintPlanOverviewDto> CreateYearPlanAsync(Guid boardId, CreateSprintPlanDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await _boardService.EnsureBoardRoleAsync(boardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);
        await _boardService.EnsureTeamModeAsync(boardId, cancellationToken);

        if (dto.DefaultDurationWeeks < 1 || dto.DefaultDurationWeeks > 16)
            throw new ValidationException(ErrorCodes.InvalidSprintDuration, "Sprint duration must be between 1 and 16 weeks.");

        if (dto.SprintCount < 1 || dto.SprintCount > 104)
            throw new ValidationException(ErrorCodes.InvalidSprintDuration, "Sprint count must be between 1 and 104.");

        // Find the highest existing PlannedOrder for this board
        var maxOrder = await _db.Sprints
            .Where(s => s.BoardId == boardId && s.PlannedOrder.HasValue)
            .MaxAsync(s => (int?)s.PlannedOrder, cancellationToken) ?? 0;

        var startDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
        var sprints = new List<Sprint>(dto.SprintCount);
        var currentStart = startDate;

        for (var i = 0; i < dto.SprintCount; i++)
        {
            var order = maxOrder + i + 1;
            var endDate = currentStart.AddDays(dto.DefaultDurationWeeks * 7);

            var sprint = new Sprint
            {
                BoardId = boardId,
                Title = $"Sprint {order}",
                StartDate = currentStart,
                EndDate = endDate,
                DurationWeeks = dto.DefaultDurationWeeks,
                PlannedOrder = order,
                Status = SprintStatus.Planning
            };

            sprints.Add(sprint);
            currentStart = endDate;
        }

        _db.Sprints.AddRange(sprints);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Year plan created for board {BoardId}: {Count} sprints starting {StartDate} by user {UserId}",
            boardId, dto.SprintCount, dto.StartDate, caller.UserId);

        await _activityService.LogAsync(boardId, caller.UserId, "sprint_plan.created", "Board", boardId,
            $"{{\"sprintCount\":{dto.SprintCount},\"durationWeeks\":{dto.DefaultDurationWeeks}}}", cancellationToken);

        return await GetPlanOverviewAsync(boardId, caller, cancellationToken);
    }

    /// <summary>
    /// Adjusts a sprint's duration and optionally its start date, cascading date changes to subsequent sprints.
    /// </summary>
    public async Task<SprintPlanOverviewDto> AdjustSprintAsync(Guid sprintId, AdjustSprintDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.DurationWeeks < 1 || dto.DurationWeeks > 16)
            throw new ValidationException(ErrorCodes.InvalidSprintDuration, "Sprint duration must be between 1 and 16 weeks.");

        var sprint = await _db.Sprints
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.SprintNotFound, "Sprint not found.");

        await _boardService.EnsureBoardRoleAsync(sprint.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);
        await _boardService.EnsureTeamModeAsync(sprint.BoardId, cancellationToken);

        // Update the adjusted sprint
        if (dto.StartDate.HasValue)
            sprint.StartDate = DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Utc);

        sprint.DurationWeeks = dto.DurationWeeks;
        sprint.EndDate = sprint.StartDate?.AddDays(dto.DurationWeeks * 7);
        sprint.UpdatedAt = DateTime.UtcNow;

        // Cascade to subsequent sprints (by PlannedOrder)
        if (sprint.PlannedOrder.HasValue && sprint.EndDate.HasValue)
        {
            var subsequentSprints = await _db.Sprints
                .Where(s => s.BoardId == sprint.BoardId
                    && s.PlannedOrder.HasValue
                    && s.PlannedOrder > sprint.PlannedOrder)
                .OrderBy(s => s.PlannedOrder)
                .ToListAsync(cancellationToken);

            var nextStart = sprint.EndDate.Value;
            foreach (var subsequent in subsequentSprints)
            {
                subsequent.StartDate = nextStart;
                subsequent.EndDate = subsequent.DurationWeeks.HasValue
                    ? nextStart.AddDays(subsequent.DurationWeeks.Value * 7)
                    : null;
                subsequent.UpdatedAt = DateTime.UtcNow;

                if (subsequent.EndDate.HasValue)
                    nextStart = subsequent.EndDate.Value;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Sprint {SprintId} adjusted to {DurationWeeks} weeks by user {UserId}",
            sprintId, dto.DurationWeeks, caller.UserId);

        return await GetPlanOverviewAsync(sprint.BoardId, caller, cancellationToken);
    }

    /// <summary>
    /// Returns a plan overview with all sprints ordered by PlannedOrder for timeline display.
    /// </summary>
    public async Task<SprintPlanOverviewDto> GetPlanOverviewAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        var sprints = await _db.Sprints
            .AsNoTracking()
            .Include(s => s.SprintCards)
                .ThenInclude(sc => sc.Card)
                    .ThenInclude(c => c!.Swimlane)
            .Where(s => s.BoardId == boardId)
            .OrderBy(s => s.PlannedOrder ?? int.MaxValue)
                .ThenBy(s => s.StartDate)
            .ToListAsync(cancellationToken);

        var sprintDtos = sprints.Select(s =>
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
                CompletedStoryPoints = cards.Where(c => (c.IsArchived || (c.Swimlane?.IsDone ?? false)) && c.StoryPoints.HasValue).Sum(c => c.StoryPoints!.Value),
                TargetStoryPoints = s.TargetStoryPoints,
                DurationWeeks = s.DurationWeeks,
                PlannedOrder = s.PlannedOrder,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            };
        }).ToList();

        return new SprintPlanOverviewDto
        {
            BoardId = boardId,
            Sprints = sprintDtos,
            TotalWeeks = sprintDtos.Where(s => s.DurationWeeks.HasValue).Sum(s => s.DurationWeeks!.Value),
            PlanStartDate = sprintDtos.FirstOrDefault()?.StartDate,
            PlanEndDate = sprintDtos.LastOrDefault()?.EndDate
        };
    }
}
