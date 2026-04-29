using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class SprintService
{
    private readonly TracksDbContext _db;

    public SprintService(TracksDbContext db) => _db = db;

    public async Task<SprintDto> CreateSprintAsync(Guid epicId, CreateSprintDto dto, CancellationToken ct)
    {
        var epic = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == epicId && wi.Type == WorkItemType.Epic && !wi.IsDeleted, ct)
            ?? throw new ValidationException("EpicId", "Epic not found or is not an Epic.");

        var maxOrder = await _db.Sprints
            .Where(s => s.EpicId == epicId)
            .MaxAsync(s => (int?)s.PlannedOrder, ct) ?? 0;

        var sprint = new Sprint
        {
            EpicId = epicId,
            Title = dto.Title,
            Goal = dto.Goal,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            TargetStoryPoints = dto.TargetStoryPoints,
            DurationWeeks = dto.DurationWeeks,
            PlannedOrder = maxOrder + 1,
            Status = SprintStatus.Planning,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync(ct);

        return MapToDto(sprint);
    }

    public async Task<SprintDto?> GetSprintAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await _db.Sprints
            .Include(s => s.SprintItems)
            .FirstOrDefaultAsync(s => s.Id == sprintId, ct);

        return sprint is null ? null : MapToDto(sprint);
    }

    public async Task<List<SprintDto>> GetSprintsByEpicAsync(Guid epicId, CancellationToken ct)
    {
        var sprints = await _db.Sprints
            .Include(s => s.SprintItems)
            .Where(s => s.EpicId == epicId)
            .OrderBy(s => s.PlannedOrder)
            .ToListAsync(ct);

        return sprints.Select(s => MapToDto(s)).ToList();
    }

    public async Task<SprintDto> UpdateSprintAsync(Guid sprintId, UpdateSprintDto dto, CancellationToken ct)
    {
        var sprint = await _db.Sprints.FindAsync([sprintId], ct)
            ?? throw new NotFoundException("Sprint", sprintId);

        if (dto.Title is not null)
            sprint.Title = dto.Title;
        if (dto.Goal is not null)
            sprint.Goal = dto.Goal;
        if (dto.StartDate is not null)
            sprint.StartDate = dto.StartDate;
        if (dto.EndDate is not null)
            sprint.EndDate = dto.EndDate;
        if (dto.TargetStoryPoints is not null)
            sprint.TargetStoryPoints = dto.TargetStoryPoints;

        sprint.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var itemCount = await _db.SprintItems.CountAsync(si => si.SprintId == sprintId, ct);
        return MapToDto(sprint, itemCount);
    }

    public async Task DeleteSprintAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await _db.Sprints
            .Include(s => s.SprintItems)
            .FirstOrDefaultAsync(s => s.Id == sprintId, ct)
            ?? throw new NotFoundException("Sprint", sprintId);

        _db.Sprints.Remove(sprint);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SprintDto> StartSprintAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await _db.Sprints.FindAsync([sprintId], ct)
            ?? throw new NotFoundException("Sprint", sprintId);

        if (sprint.Status != SprintStatus.Planning)
            throw new System.InvalidOperationException("Only sprints in Planning status can be started.");

        sprint.Status = SprintStatus.Active;
        sprint.StartDate ??= DateTime.UtcNow;
        sprint.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var itemCount = await _db.SprintItems.CountAsync(si => si.SprintId == sprintId, ct);
        return MapToDto(sprint, itemCount);
    }

    public async Task<SprintDto> CompleteSprintAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await _db.Sprints.FindAsync([sprintId], ct)
            ?? throw new NotFoundException("Sprint", sprintId);

        if (sprint.Status != SprintStatus.Active)
            throw new System.InvalidOperationException("Only active sprints can be completed.");

        sprint.Status = SprintStatus.Completed;
        sprint.EndDate ??= DateTime.UtcNow;
        sprint.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var itemCount = await _db.SprintItems.CountAsync(si => si.SprintId == sprintId, ct);
        return MapToDto(sprint, itemCount);
    }

    public async Task AddItemToSprintAsync(Guid sprintId, Guid itemId, CancellationToken ct)
    {
        var sprint = await _db.Sprints.FindAsync([sprintId], ct)
            ?? throw new NotFoundException("Sprint", sprintId);

        var item = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == itemId && wi.Type == WorkItemType.Item && !wi.IsDeleted, ct)
            ?? throw new ValidationException("ItemId", "Item not found or is not an Item.");

        var exists = await _db.SprintItems
            .AnyAsync(si => si.SprintId == sprintId && si.ItemId == itemId, ct);

        if (exists)
            throw new ValidationException("ItemId", "Item is already in this sprint.");

        var sprintItem = new SprintItem
        {
            SprintId = sprintId,
            ItemId = itemId,
            AddedAt = DateTime.UtcNow
        };

        _db.SprintItems.Add(sprintItem);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveItemFromSprintAsync(Guid sprintId, Guid itemId, CancellationToken ct)
    {
        var sprintItem = await _db.SprintItems
            .FirstOrDefaultAsync(si => si.SprintId == sprintId && si.ItemId == itemId, ct)
            ?? throw new NotFoundException("SprintItem", $"{sprintId}/{itemId}");

        _db.SprintItems.Remove(sprintItem);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<WorkItemDto>> GetBacklogItemsAsync(Guid epicId, CancellationToken ct)
    {
        // Get all Item-type work items that are descendants of Features under this Epic
        // and are not assigned to any non-Completed sprint.
        var nonCompletedSprintIds = await _db.Sprints
            .Where(s => s.EpicId == epicId && s.Status != SprintStatus.Completed)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var assignedItemIds = nonCompletedSprintIds.Count > 0
            ? await _db.SprintItems
                .Where(si => nonCompletedSprintIds.Contains(si.SprintId))
                .Select(si => si.ItemId)
                .ToListAsync(ct)
            : new List<Guid>();

        var featureIds = await _db.WorkItems
            .Where(wi => wi.ParentWorkItemId == epicId && wi.Type == WorkItemType.Feature && !wi.IsDeleted && !wi.IsArchived)
            .Select(wi => wi.Id)
            .ToListAsync(ct);

        var items = await _db.WorkItems
            .Where(wi => featureIds.Contains(wi.ParentWorkItemId!.Value)
                && wi.Type == WorkItemType.Item
                && !wi.IsDeleted
                && !wi.IsArchived
                && !assignedItemIds.Contains(wi.Id))
            .OrderBy(wi => wi.Position)
            .ToListAsync(ct);

        return items.Select(MapWorkItemToDto).ToList();
    }

    private static SprintDto MapToDto(Sprint sprint, int? itemCountOverride = null)
    {
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
            ItemCount = itemCountOverride ?? sprint.SprintItems.Count,
            CreatedAt = sprint.CreatedAt,
            UpdatedAt = sprint.UpdatedAt
        };
    }

    private static WorkItemDto MapWorkItemToDto(WorkItem wi)
    {
        return new WorkItemDto
        {
            Id = wi.Id,
            ProductId = wi.ProductId,
            ParentWorkItemId = wi.ParentWorkItemId,
            Type = wi.Type,
            SwimlaneId = wi.SwimlaneId,
            SwimlaneTitle = wi.Swimlane?.Title,
            ItemNumber = wi.ItemNumber,
            Title = wi.Title,
            Description = wi.Description,
            Position = wi.Position,
            Priority = wi.Priority,
            DueDate = wi.DueDate,
            StoryPoints = wi.StoryPoints,
            IsArchived = wi.IsArchived,
            CommentCount = wi.Comments.Count,
            AttachmentCount = wi.Attachments.Count,
            Assignments = wi.Assignments.Select(a => new WorkItemAssignmentDto
            {
                UserId = a.UserId,
                DisplayName = null,
                AssignedAt = a.AssignedAt
            }).ToList(),
            Labels = wi.WorkItemLabels.Select(wl => new LabelDto
            {
                Id = wl.LabelId,
                ProductId = wi.ProductId,
                Title = wl.Label?.Title ?? string.Empty,
                Color = wl.Label?.Color ?? string.Empty,
                CreatedAt = wl.Label?.CreatedAt ?? DateTime.UtcNow
            }).ToList(),
            ChildWorkItems = null,
            Checklists = null,
            SprintId = null,
            SprintTitle = null,
            TotalTrackedMinutes = 0,
            ETag = wi.ETag,
            CreatedAt = wi.CreatedAt,
            UpdatedAt = wi.UpdatedAt
        };
    }
}
