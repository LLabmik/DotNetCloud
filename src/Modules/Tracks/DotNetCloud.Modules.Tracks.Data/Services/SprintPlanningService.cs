using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class SprintPlanningService
{
    private readonly TracksDbContext _db;

    public SprintPlanningService(TracksDbContext db) => _db = db;

    public async Task<List<SprintDto>> CreateSprintPlanAsync(Guid epicId, CreateSprintPlanDto dto, CancellationToken ct)
    {
        var epic = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == epicId && wi.Type == WorkItemType.Epic && !wi.IsDeleted, ct)
            ?? throw new ValidationException("EpicId", "Epic not found or is not an Epic.");

        if (dto.NumberOfSprints <= 0)
            throw new ValidationException("NumberOfSprints", "Number of sprints must be greater than zero.");

        if (dto.SprintDurationWeeks is < 1 or > 16)
            throw new ValidationException("SprintDurationWeeks", "Sprint duration must be between 1 and 16 weeks.");

        var maxOrder = await _db.Sprints
            .Where(s => s.EpicId == epicId)
            .MaxAsync(s => (int?)s.PlannedOrder, ct) ?? 0;

        var startDate = dto.StartDate ?? DateTime.UtcNow;
        var sprints = new List<Sprint>();

        for (int i = 0; i < dto.NumberOfSprints; i++)
        {
            var sprintStart = startDate.AddDays(7 * dto.SprintDurationWeeks * i);
            var sprintEnd = sprintStart.AddDays(7 * dto.SprintDurationWeeks - 1);

            var sprint = new Sprint
            {
                EpicId = epicId,
                Title = $"Sprint {maxOrder + i + 1}",
                StartDate = sprintStart,
                EndDate = sprintEnd,
                DurationWeeks = dto.SprintDurationWeeks,
                PlannedOrder = maxOrder + i + 1,
                Status = SprintStatus.Planning,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Sprints.Add(sprint);
            sprints.Add(sprint);
        }

        await _db.SaveChangesAsync(ct);

        return sprints.Select(s => new SprintDto
        {
            Id = s.Id,
            EpicId = s.EpicId,
            Title = s.Title,
            Goal = s.Goal,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            Status = s.Status,
            TargetStoryPoints = s.TargetStoryPoints,
            DurationWeeks = s.DurationWeeks,
            PlannedOrder = s.PlannedOrder,
            ItemCount = 0,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();
    }

    public async Task<List<SprintDto>> GetSprintPlanAsync(Guid epicId, CancellationToken ct)
    {
        var sprints = await _db.Sprints
            .Include(s => s.SprintItems)
            .Where(s => s.EpicId == epicId)
            .OrderBy(s => s.PlannedOrder)
            .ToListAsync(ct);

        return sprints.Select(s => new SprintDto
        {
            Id = s.Id,
            EpicId = s.EpicId,
            Title = s.Title,
            Goal = s.Goal,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            Status = s.Status,
            TargetStoryPoints = s.TargetStoryPoints,
            DurationWeeks = s.DurationWeeks,
            PlannedOrder = s.PlannedOrder,
            ItemCount = s.SprintItems.Count,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();
    }

    public async Task<SprintDto> AdjustSprintDatesAsync(Guid sprintId, AdjustSprintDto dto, CancellationToken ct)
    {
        var sprint = await _db.Sprints.FindAsync([sprintId], ct)
            ?? throw new NotFoundException("Sprint", sprintId);

        var oldStartDate = sprint.StartDate;
        var oldEndDate = sprint.EndDate;

        if (dto.DurationWeeks is not null)
        {
            if (dto.DurationWeeks is < 1 or > 16)
                throw new ValidationException("DurationWeeks", "Sprint duration must be between 1 and 16 weeks.");

            sprint.DurationWeeks = dto.DurationWeeks;
        }

        if (dto.StartDate is not null)
            sprint.StartDate = dto.StartDate;

        if (dto.EndDate is not null)
            sprint.EndDate = dto.EndDate;

        // If duration changed but end date was not explicitly set, recalculate end date
        if (dto.DurationWeeks is not null && dto.EndDate is null && sprint.StartDate is not null)
        {
            sprint.EndDate = sprint.StartDate.Value.AddDays(7 * sprint.DurationWeeks!.Value - 1);
        }

        sprint.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Cascade date shifts to subsequent sprints
        if (dto.DurationWeeks is not null || dto.StartDate is not null || dto.EndDate is not null)
        {
            var subsequentSprints = await _db.Sprints
                .Where(s => s.EpicId == sprint.EpicId && s.PlannedOrder > sprint.PlannedOrder)
                .OrderBy(s => s.PlannedOrder)
                .ToListAsync(ct);

            if (subsequentSprints.Count > 0)
            {
                var daysShift = sprint.EndDate.HasValue && oldEndDate.HasValue
                    ? (sprint.EndDate.Value - oldEndDate.Value).Days
                    : 0;

                foreach (var subsequent in subsequentSprints)
                {
                    if (subsequent.StartDate is not null)
                        subsequent.StartDate = subsequent.StartDate.Value.AddDays(daysShift);

                    if (subsequent.EndDate is not null)
                        subsequent.EndDate = subsequent.EndDate.Value.AddDays(daysShift);

                    subsequent.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync(ct);
            }
        }

        var itemCount = await _db.SprintItems.CountAsync(si => si.SprintId == sprintId, ct);
        return new SprintDto
        {
            Id = sprint.Id,
            EpicId = sprint.EpicId,
            Title = sprint.Title,
            Goal = sprint.Goal,
            StartDate = sprint.StartDate,
            EndDate = sprint.EndDate,
            Status = sprint.Status,
            TargetStoryPoints = sprint.TargetStoryPoints,
            DurationWeeks = sprint.DurationWeeks,
            PlannedOrder = sprint.PlannedOrder,
            ItemCount = itemCount,
            CreatedAt = sprint.CreatedAt,
            UpdatedAt = sprint.UpdatedAt
        };
    }
}
