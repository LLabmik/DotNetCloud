using DotNetCloud.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class AnalyticsService
{
    private readonly TracksDbContext _db;

    public AnalyticsService(TracksDbContext db)
    {
        _db = db;
    }

    public async Task<ProductAnalyticsDto> GetProductAnalyticsAsync(Guid productId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);

        var nonDeletedItems = _db.WorkItems.Where(wi => wi.ProductId == productId && !wi.IsDeleted);

        var totalItems = await nonDeletedItems.CountAsync(ct);
        var totalEpics = await nonDeletedItems.CountAsync(wi => wi.Type == WorkItemType.Epic, ct);
        var totalFeatures = await nonDeletedItems.CountAsync(wi => wi.Type == WorkItemType.Feature, ct);

        // Active sprints: sprints with Status=Active whose Epic belongs to this product
        var activeSprints = await _db.Sprints
            .Where(s => s.Status == SprintStatus.Active && s.Epic!.ProductId == productId)
            .CountAsync(ct);

        // Done swimlane IDs for this product
        var doneSwimlaneIds = await _db.Swimlanes
            .Where(s => s.ContainerType == SwimlaneContainerType.Product && s.ContainerId == productId && s.IsDone)
            .Select(s => s.Id)
            .ToListAsync(ct);

        // Completed items: non-deleted items currently in a done swimlane
        var completedItems = await nonDeletedItems
            .Where(wi => wi.SwimlaneId != null && doneSwimlaneIds.Contains(wi.SwimlaneId.Value))
            .Select(wi => new { wi.Id, wi.CreatedAt })
            .ToListAsync(ct);

        var completedItemIds = completedItems.Select(ci => ci.Id).ToHashSet();

        // Items completed this week: completed items whose most recent move-to-done activity was in last 7 days.
        // We use Activity records as the source of truth for when a move happened.
        var activitiesLastWeek = await _db.Activities
            .Where(a => a.EntityType == "WorkItem"
                && a.ProductId == productId
                && a.CreatedAt >= weekAgo
                && completedItemIds.Contains(a.EntityId)
                && a.Action.Contains("Move"))
            .Select(a => new { a.EntityId, a.CreatedAt })
            .ToListAsync(ct);

        // Count distinct items that had a move activity in the last week and are currently done
        var itemsCompletedThisWeek = activitiesLastWeek
            .GroupBy(a => a.EntityId)
            .Count(g => g.Any(a => a.CreatedAt >= weekAgo));

        // Average cycle time: for each completed item, find the time from CreatedAt to completion move
        var completionDates = await GetCompletionDatesAsync(completedItemIds, productId, ct);

        double avgCycleTimeDays = 0;
        if (completedItems.Count > 0)
        {
            var cycleTimes = new List<double>();
            foreach (var ci in completedItems)
            {
                if (completionDates.TryGetValue(ci.Id, out var completionDate))
                {
                    cycleTimes.Add((completionDate - ci.CreatedAt).TotalDays);
                }
            }
            avgCycleTimeDays = cycleTimes.Count > 0 ? cycleTimes.Average() : 0;
        }

        // Daily completions: items completed per day in the last 30 days
        var dailyCompletions = new List<DailyCompletionDto>();
        var completionsByDay = completionDates
            .Where(kv => kv.Value >= thirtyDaysAgo)
            .GroupBy(kv => kv.Value.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        for (var day = thirtyDaysAgo.Date; day <= now.Date; day = day.AddDays(1))
        {
            completionsByDay.TryGetValue(day, out var count);
            dailyCompletions.Add(new DailyCompletionDto
            {
                Date = day,
                CompletedCount = count
            });
        }

        return new ProductAnalyticsDto
        {
            TotalItems = totalItems,
            TotalEpics = totalEpics,
            TotalFeatures = totalFeatures,
            ItemsCompletedThisWeek = itemsCompletedThisWeek,
            ActiveSprints = activeSprints,
            AvgCycleTimeDays = Math.Round(avgCycleTimeDays, 1),
            DailyCompletions = dailyCompletions
        };
    }

    public async Task<List<SprintVelocityDto>> GetVelocityDataAsync(Guid productId, CancellationToken ct)
    {
        // Get completed sprints for this product (via Epic), last 10
        var completedSprints = await _db.Sprints
            .Where(s => s.Status == SprintStatus.Completed && s.Epic!.ProductId == productId)
            .OrderByDescending(s => s.EndDate)
            .Take(10)
            .Select(s => new { s.Id, s.Title })
            .ToListAsync(ct);

        var result = new List<SprintVelocityDto>();

        foreach (var sprint in completedSprints)
        {
            var sprintItemIds = await _db.SprintItems
                .Where(si => si.SprintId == sprint.Id)
                .Select(si => si.ItemId)
                .ToListAsync(ct);

            var sprintWorkItems = await _db.WorkItems
                .Where(wi => sprintItemIds.Contains(wi.Id) && !wi.IsDeleted)
                .Select(wi => new { wi.Id, wi.StoryPoints, wi.SwimlaneId })
                .ToListAsync(ct);

            var doneSwimlaneIds = await _db.Swimlanes
                .Where(s => s.ContainerType == SwimlaneContainerType.Product && s.ContainerId == productId && s.IsDone)
                .Select(s => s.Id)
                .ToListAsync(ct);

            var completedPoints = sprintWorkItems
                .Where(wi => wi.SwimlaneId != null && doneSwimlaneIds.Contains(wi.SwimlaneId.Value))
                .Sum(wi => wi.StoryPoints ?? 0);

            var totalPoints = sprintWorkItems.Sum(wi => wi.StoryPoints ?? 0);

            result.Add(new SprintVelocityDto
            {
                SprintId = sprint.Id,
                SprintTitle = sprint.Title,
                CompletedStoryPoints = completedPoints,
                TotalStoryPoints = totalPoints
            });
        }

        return result;
    }

    public async Task<SprintReportDto> GetSprintReportAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await _db.Sprints
            .Include(s => s.Epic)
            .FirstOrDefaultAsync(s => s.Id == sprintId, ct);

        if (sprint is null)
            throw new InvalidOperationException($"Sprint with ID {sprintId} not found.");

        var productId = sprint.Epic!.ProductId;

        var sprintItemIds = await _db.SprintItems
            .Where(si => si.SprintId == sprintId)
            .Select(si => si.ItemId)
            .ToListAsync(ct);

        var sprintWorkItems = await _db.WorkItems
            .Where(wi => sprintItemIds.Contains(wi.Id) && !wi.IsDeleted)
            .Select(wi => new { wi.Id, wi.StoryPoints, wi.SwimlaneId })
            .ToListAsync(ct);

        var doneSwimlaneIds = await _db.Swimlanes
            .Where(s => s.ContainerType == SwimlaneContainerType.Product && s.ContainerId == productId && s.IsDone)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var completedItems = sprintWorkItems.Count(wi => wi.SwimlaneId != null && doneSwimlaneIds.Contains(wi.SwimlaneId.Value));
        var incompleteItems = sprintWorkItems.Count - completedItems;
        var completedStoryPoints = sprintWorkItems
            .Where(wi => wi.SwimlaneId != null && doneSwimlaneIds.Contains(wi.SwimlaneId.Value))
            .Sum(wi => wi.StoryPoints ?? 0);
        var totalStoryPoints = sprintWorkItems.Sum(wi => wi.StoryPoints ?? 0);

        var sprintDto = new SprintDto
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
            ItemCount = sprintItemIds.Count,
            CreatedAt = sprint.CreatedAt,
            UpdatedAt = sprint.UpdatedAt
        };

        return new SprintReportDto
        {
            Sprint = sprintDto,
            CompletedItems = completedItems,
            IncompleteItems = incompleteItems,
            CompletedStoryPoints = completedStoryPoints,
            TotalStoryPoints = totalStoryPoints
        };
    }

    public async Task<SprintBurndownDto> GetBurndownDataAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await _db.Sprints
            .Include(s => s.Epic)
            .FirstOrDefaultAsync(s => s.Id == sprintId, ct);

        if (sprint is null)
            throw new InvalidOperationException($"Sprint with ID {sprintId} not found.");

        var productId = sprint.Epic!.ProductId;

        var sprintItemIds = await _db.SprintItems
            .Where(si => si.SprintId == sprintId)
            .Select(si => si.ItemId)
            .ToListAsync(ct);

        var sprintWorkItems = await _db.WorkItems
            .Where(wi => sprintItemIds.Contains(wi.Id) && !wi.IsDeleted)
            .Select(wi => new { wi.Id, wi.StoryPoints })
            .ToListAsync(ct);

        var totalStoryPoints = sprintWorkItems.Sum(wi => wi.StoryPoints ?? 0);

        // Get done swimlane IDs for the product
        var doneSwimlaneIds = await _db.Swimlanes
            .Where(s => s.ContainerType == SwimlaneContainerType.Product && s.ContainerId == productId && s.IsDone)
            .Select(s => s.Id)
            .ToListAsync(ct);

        // Find completion dates via Activity records for items moved to done swimlanes
        var completionDates = await GetCompletionDatesAsync(sprintItemIds.ToHashSet(), productId, ct);

        // Filter to only items currently in a done swimlane
        var doneItemIds = await _db.WorkItems
            .Where(wi => sprintItemIds.Contains(wi.Id) && !wi.IsDeleted && wi.SwimlaneId != null && doneSwimlaneIds.Contains(wi.SwimlaneId.Value))
            .Select(wi => wi.Id)
            .ToListAsync(ct);

        var doneItemIdSet = doneItemIds.ToHashSet();

        // Calculate remaining story points for each day of the sprint
        var points = new List<BurndownPointDto>();
        var startDate = sprint.StartDate?.Date ?? sprint.CreatedAt.Date;
        var endDate = sprint.EndDate?.Date ?? DateTime.UtcNow.Date;
        if (endDate < startDate) endDate = startDate;

        // Calculate story points per item
        var itemPoints = sprintWorkItems.ToDictionary(wi => wi.Id, wi => wi.StoryPoints ?? 0);

        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            // Items completed on or before this day
            var completedPoints = 0;
            foreach (var itemId in doneItemIdSet)
            {
                if (completionDates.TryGetValue(itemId, out var completedAt) && completedAt.Date <= day)
                {
                    completedPoints += itemPoints.GetValueOrDefault(itemId, 0);
                }
            }

            var remaining = totalStoryPoints - completedPoints;
            points.Add(new BurndownPointDto
            {
                Date = day,
                RemainingStoryPoints = Math.Max(0, remaining)
            });
        }

        return new SprintBurndownDto
        {
            TotalStoryPoints = totalStoryPoints,
            Points = points
        };
    }

    /// <summary>
    /// Returns a dictionary mapping item ID to the date it was moved to a done swimlane,
    /// based on Activity records.
    /// </summary>
    private async Task<Dictionary<Guid, DateTime>> GetCompletionDatesAsync(HashSet<Guid> itemIds, Guid productId, CancellationToken ct)
    {
        if (itemIds.Count == 0)
            return new Dictionary<Guid, DateTime>();

        // Find activities where these items were moved to done swimlanes.
        // We look for Activity records with EntityType="WorkItem" and Action containing "Move"
        // as these track swimlane changes. The most recent such activity before the item
        // entered its done swimlane represents the completion date.
        var activities = await _db.Activities
            .Where(a => a.EntityType == "WorkItem"
                && a.ProductId == productId
                && itemIds.Contains(a.EntityId)
                && a.Action.Contains("Move"))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.EntityId, a.CreatedAt })
            .ToListAsync(ct);

        // For each item, take the most recent move activity as the completion date
        var result = new Dictionary<Guid, DateTime>();
        foreach (var activity in activities)
        {
            if (!result.ContainsKey(activity.EntityId))
            {
                result[activity.EntityId] = activity.CreatedAt;
            }
        }

        return result;
    }
}
