using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class WorkItemService
{
    private readonly TracksDbContext _db;

    public WorkItemService(TracksDbContext db)
    {
        _db = db;
    }

    public async Task<WorkItemDto> CreateWorkItemAsync(
        Guid productId,
        Guid swimlaneId,
        WorkItemType type,
        Guid createdByUserId,
        CreateWorkItemDto dto,
        CancellationToken ct)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        var swimlane = await _db.Swimlanes
            .FirstOrDefaultAsync(s => s.Id == swimlaneId, ct)
            ?? throw new InvalidOperationException($"Swimlane {swimlaneId} not found.");

        Guid? parentWorkItemId = null;

        if (swimlane.ContainerType == SwimlaneContainerType.WorkItem)
        {
            parentWorkItemId = swimlane.ContainerId;

            var parent = await _db.WorkItems
                .FirstOrDefaultAsync(wi => wi.Id == parentWorkItemId.Value, ct)
                ?? throw new InvalidOperationException($"Parent work item {parentWorkItemId} not found.");

            ValidateHierarchy(type, parent.Type, product);
        }
        else
        {
            if (type != WorkItemType.Epic)
                throw new InvalidOperationException(
                    $"Work items of type {type} must be created within a parent work item's swimlane, not a product-level swimlane.");

            if (swimlane.ContainerId != productId)
                throw new InvalidOperationException("Swimlane does not belong to the specified product.");
        }

        var maxNumber = await _db.WorkItems
            .Where(wi => wi.ProductId == productId)
            .MaxAsync(wi => (int?)wi.ItemNumber, ct) ?? 0;

        var itemNumber = maxNumber + 1;

        var maxPosition = await _db.WorkItems
            .Where(wi => wi.SwimlaneId == swimlaneId)
            .MaxAsync(wi => (double?)wi.Position, ct) ?? 0;

        var position = maxPosition > 0 ? maxPosition + 1024 : 1000;

        var workItem = new WorkItem
        {
            ProductId = productId,
            ParentWorkItemId = parentWorkItemId,
            Type = type,
            SwimlaneId = swimlaneId,
            ItemNumber = itemNumber,
            Title = dto.Title,
            Description = dto.Description,
            Position = position,
            Priority = dto.Priority,
            DueDate = dto.DueDate,
            StoryPoints = dto.StoryPoints,
            CreatedByUserId = createdByUserId
        };

        _db.WorkItems.Add(workItem);

        // Auto-subscribe the creator so they get notified of changes
        _db.WorkItemWatchers.Add(new WorkItemWatcher
        {
            WorkItemId = workItem.Id,
            UserId = createdByUserId,
            SubscribedAt = DateTime.UtcNow
        });

        if (dto.AssigneeIds is { Count: > 0 })
        {
            foreach (var userId in dto.AssigneeIds)
            {
                _db.WorkItemAssignments.Add(new WorkItemAssignment
                {
                    WorkItemId = workItem.Id,
                    UserId = userId
                });

                // Auto-subscribe assignees so they get notified of changes
                if (userId != createdByUserId)
                {
                    _db.WorkItemWatchers.Add(new WorkItemWatcher
                    {
                        WorkItemId = workItem.Id,
                        UserId = userId,
                        SubscribedAt = DateTime.UtcNow
                    });
                }
            }
        }

        if (dto.LabelIds is { Count: > 0 })
        {
            foreach (var labelId in dto.LabelIds)
            {
                var labelExists = await _db.Labels
                    .AnyAsync(l => l.Id == labelId && l.ProductId == productId, ct);

                if (!labelExists)
                    throw new InvalidOperationException($"Label {labelId} not found in product {productId}.");

                _db.WorkItemLabels.Add(new WorkItemLabel
                {
                    WorkItemId = workItem.Id,
                    LabelId = labelId
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        return MapToDto(workItem, swimlane.Title, new List<WorkItemAssignmentDto>(),
            new List<LabelDto>(), commentCount: 0, attachmentCount: 0);
    }

    public async Task<WorkItemDto> GetWorkItemAsync(Guid workItemId, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .Include(wi => wi.Product)
            .Include(wi => wi.Swimlane)
            .Include(wi => wi.Assignments)
            .Include(wi => wi.WorkItemLabels)
                .ThenInclude(wl => wl.Label)
            .FirstOrDefaultAsync(wi => wi.Id == workItemId, ct)
            ?? throw new InvalidOperationException($"Work item {workItemId} not found.");

        var commentCount = await _db.WorkItemComments
            .CountAsync(c => c.WorkItemId == workItemId && !c.IsDeleted, ct);

        var attachmentCount = await _db.WorkItemAttachments
            .CountAsync(a => a.WorkItemId == workItemId, ct);

        List<WorkItemDto>? childWorkItems = null;

        if (workItem.Type == WorkItemType.Epic
            || workItem.Type == WorkItemType.Feature
            || workItem.Type == WorkItemType.Item)
        {
            var children = await _db.WorkItems
                .Where(wi => wi.ParentWorkItemId == workItemId)
                .Include(wi => wi.Swimlane)
                .OrderBy(wi => wi.Position)
                .ToListAsync(ct);

            childWorkItems = children.Select(MapToChildDto).ToList();
        }

        List<ChecklistDto>? checklists = null;

        if (workItem.Type == WorkItemType.Item
            && workItem.Product is not null
            && !workItem.Product.SubItemsEnabled)
        {
            checklists = await _db.Checklists
                .Where(c => c.ItemId == workItemId)
                .Include(c => c.Items.OrderBy(ci => ci.Position))
                .OrderBy(c => c.Position)
                .Select(c => new ChecklistDto
                {
                    Id = c.Id,
                    ItemId = c.ItemId,
                    Title = c.Title,
                    Position = c.Position,
                    Items = c.Items.Select(ci => new ChecklistItemDto
                    {
                        Id = ci.Id,
                        ChecklistId = ci.ChecklistId,
                        Title = ci.Title,
                        IsCompleted = ci.IsCompleted,
                        Position = ci.Position,
                        AssignedToUserId = ci.AssignedToUserId,
                        CreatedAt = ci.CreatedAt,
                        UpdatedAt = ci.UpdatedAt
                    }).ToList(),
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync(ct);
        }

        Guid? sprintId = null;
        string? sprintTitle = null;

        if (workItem.Type == WorkItemType.Item)
        {
            var sprintItem = await _db.SprintItems
                .Include(si => si.Sprint)
                .FirstOrDefaultAsync(si => si.ItemId == workItemId, ct);

            if (sprintItem?.Sprint is not null)
            {
                sprintId = sprintItem.Sprint.Id;
                sprintTitle = sprintItem.Sprint.Title;
            }
        }

        var assignments = workItem.Assignments
            .Select(a => new WorkItemAssignmentDto
            {
                UserId = a.UserId,
                AssignedAt = a.AssignedAt
            }).ToList();

        var labels = workItem.WorkItemLabels
            .Select(wl => new LabelDto
            {
                Id = wl.Label!.Id,
                ProductId = wl.Label.ProductId,
                Title = wl.Label.Title,
                Color = wl.Label.Color,
                CreatedAt = wl.Label.CreatedAt
            }).ToList();

        return new WorkItemDto
        {
            Id = workItem.Id,
            ProductId = workItem.ProductId,
            ParentWorkItemId = workItem.ParentWorkItemId,
            Type = workItem.Type,
            SwimlaneId = workItem.SwimlaneId,
            SwimlaneTitle = workItem.Swimlane?.Title,
            ItemNumber = workItem.ItemNumber,
            Title = workItem.Title,
            Description = workItem.Description,
            Position = workItem.Position,
            Priority = workItem.Priority,
            DueDate = workItem.DueDate,
            StoryPoints = workItem.StoryPoints,
            IsArchived = workItem.IsArchived,
            CommentCount = commentCount,
            AttachmentCount = attachmentCount,
            Assignments = assignments,
            Labels = labels,
            ChildWorkItems = childWorkItems,
            Checklists = checklists,
            SprintId = sprintId,
            SprintTitle = sprintTitle,
            ETag = workItem.ETag,
            CreatedAt = workItem.CreatedAt,
            UpdatedAt = workItem.UpdatedAt
        };
    }

    public async Task<WorkItemDto> GetWorkItemByNumberAsync(Guid productId, int itemNumber, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.ProductId == productId && wi.ItemNumber == itemNumber, ct)
            ?? throw new InvalidOperationException(
                $"Work item with number {itemNumber} not found in product {productId}.");

        return await GetWorkItemAsync(workItem.Id, ct);
    }

    public async Task<WorkItemDto> UpdateWorkItemAsync(Guid workItemId, UpdateWorkItemDto dto, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == workItemId, ct)
            ?? throw new InvalidOperationException($"Work item {workItemId} not found.");

        if (!string.IsNullOrEmpty(dto.ETag) && dto.ETag != workItem.ETag)
            throw new InvalidOperationException(
                "The work item has been modified by another user. Please refresh and try again.");

        if (dto.Title is not null)
            workItem.Title = dto.Title;
        if (dto.Description is not null)
            workItem.Description = dto.Description;
        if (dto.Priority.HasValue)
            workItem.Priority = dto.Priority.Value;
        if (dto.DueDate is not null)
            workItem.DueDate = dto.DueDate;
        if (dto.StoryPoints.HasValue)
            workItem.StoryPoints = dto.StoryPoints.Value;

        workItem.ETag = Guid.NewGuid().ToString("N");
        workItem.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetWorkItemAsync(workItemId, ct);
    }

    public async Task DeleteWorkItemAsync(Guid workItemId, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == workItemId, ct)
            ?? throw new InvalidOperationException($"Work item {workItemId} not found.");

        workItem.IsDeleted = true;
        workItem.DeletedAt = DateTime.UtcNow;
        workItem.ETag = Guid.NewGuid().ToString("N");
        workItem.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<WorkItemDto> MoveWorkItemAsync(Guid workItemId, MoveWorkItemDto dto, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == workItemId, ct)
            ?? throw new InvalidOperationException($"Work item {workItemId} not found.");

        var targetSwimlane = await _db.Swimlanes
            .FirstOrDefaultAsync(s => s.Id == dto.TargetSwimlaneId && !s.IsArchived, ct)
            ?? throw new InvalidOperationException($"Target swimlane {dto.TargetSwimlaneId} not found or is archived.");

        workItem.SwimlaneId = dto.TargetSwimlaneId;

        if (dto.Position.HasValue)
        {
            workItem.Position = dto.Position.Value;
        }
        else
        {
            var maxPosition = await _db.WorkItems
                .Where(wi => wi.SwimlaneId == dto.TargetSwimlaneId && wi.Id != workItemId)
                .MaxAsync(wi => (double?)wi.Position, ct) ?? 0;

            workItem.Position = maxPosition > 0 ? maxPosition + 1024 : 1000;
        }

        workItem.ETag = Guid.NewGuid().ToString("N");
        workItem.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetWorkItemAsync(workItemId, ct);
    }

    public async Task<List<WorkItemDto>> GetWorkItemsBySwimlaneAsync(Guid swimlaneId, CancellationToken ct)
    {
        var workItems = await _db.WorkItems
            .Where(wi => wi.SwimlaneId == swimlaneId && !wi.IsArchived)
            .Include(wi => wi.Swimlane)
            .OrderBy(wi => wi.Position)
            .ToListAsync(ct);

        return workItems.Select(wi => MapToDto(
            wi,
            wi.Swimlane?.Title,
            new List<WorkItemAssignmentDto>(),
            new List<LabelDto>(),
            commentCount: 0,
            attachmentCount: 0)).ToList();
    }

    public async Task<List<WorkItemDto>> GetChildWorkItemsAsync(Guid parentWorkItemId, CancellationToken ct)
    {
        var children = await _db.WorkItems
            .Where(wi => wi.ParentWorkItemId == parentWorkItemId)
            .Include(wi => wi.Swimlane)
            .OrderBy(wi => wi.Position)
            .ToListAsync(ct);

        return children.Select(MapToChildDto).ToList();
    }

    public async Task<WorkItemAssignmentDto> AssignUserAsync(Guid workItemId, Guid userId, CancellationToken ct)
    {
        _ = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == workItemId, ct)
            ?? throw new InvalidOperationException($"Work item {workItemId} not found.");

        var existing = await _db.WorkItemAssignments
            .FirstOrDefaultAsync(a => a.WorkItemId == workItemId && a.UserId == userId, ct);

        if (existing is not null)
            throw new InvalidOperationException("User is already assigned to this work item.");

        var assignment = new WorkItemAssignment
        {
            WorkItemId = workItemId,
            UserId = userId
        };

        _db.WorkItemAssignments.Add(assignment);

        await _db.SaveChangesAsync(ct);

        return new WorkItemAssignmentDto
        {
            UserId = assignment.UserId,
            AssignedAt = assignment.AssignedAt
        };
    }

    public async Task RemoveAssignmentAsync(Guid workItemId, Guid userId, CancellationToken ct)
    {
        var assignment = await _db.WorkItemAssignments
            .FirstOrDefaultAsync(a => a.WorkItemId == workItemId && a.UserId == userId, ct)
            ?? throw new InvalidOperationException("User is not assigned to this work item.");

        _db.WorkItemAssignments.Remove(assignment);

        await _db.SaveChangesAsync(ct);
    }

    public async Task AddLabelAsync(Guid workItemId, Guid labelId, CancellationToken ct)
    {
        _ = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == workItemId, ct)
            ?? throw new InvalidOperationException($"Work item {workItemId} not found.");

        var label = await _db.Labels
            .FirstOrDefaultAsync(l => l.Id == labelId, ct)
            ?? throw new InvalidOperationException($"Label {labelId} not found.");

        var existing = await _db.WorkItemLabels
            .FirstOrDefaultAsync(wl => wl.WorkItemId == workItemId && wl.LabelId == labelId, ct);

        if (existing is not null)
            throw new InvalidOperationException("Label is already applied to this work item.");

        var workItemLabel = new WorkItemLabel
        {
            WorkItemId = workItemId,
            LabelId = labelId
        };

        _db.WorkItemLabels.Add(workItemLabel);

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveLabelAsync(Guid workItemId, Guid labelId, CancellationToken ct)
    {
        var workItemLabel = await _db.WorkItemLabels
            .FirstOrDefaultAsync(wl => wl.WorkItemId == workItemId && wl.LabelId == labelId, ct)
            ?? throw new InvalidOperationException("Label is not applied to this work item.");

        _db.WorkItemLabels.Remove(workItemLabel);

        await _db.SaveChangesAsync(ct);
    }

    private static void ValidateHierarchy(WorkItemType childType, WorkItemType parentType, Product product)
    {
        switch (childType)
        {
            case WorkItemType.Feature:
                if (parentType != WorkItemType.Epic)
                    throw new InvalidOperationException("Features must have an Epic as their parent.");
                break;

            case WorkItemType.Item:
                if (parentType != WorkItemType.Feature)
                    throw new InvalidOperationException("Items must have a Feature as their parent.");
                break;

            case WorkItemType.SubItem:
                if (parentType != WorkItemType.Item)
                    throw new InvalidOperationException("SubItems must have an Item as their parent.");
                if (!product.SubItemsEnabled)
                    throw new InvalidOperationException("SubItems are not enabled for this product.");
                break;

            default:
                throw new InvalidOperationException(
                    $"Work items of type {childType} cannot be created as children of {parentType}.");
        }
    }

    private static WorkItemDto MapToDto(
        WorkItem workItem,
        string? swimlaneTitle,
        List<WorkItemAssignmentDto> assignments,
        List<LabelDto> labels,
        int commentCount,
        int attachmentCount)
    {
        return new WorkItemDto
        {
            Id = workItem.Id,
            ProductId = workItem.ProductId,
            ParentWorkItemId = workItem.ParentWorkItemId,
            Type = workItem.Type,
            SwimlaneId = workItem.SwimlaneId,
            SwimlaneTitle = swimlaneTitle,
            ItemNumber = workItem.ItemNumber,
            Title = workItem.Title,
            Description = workItem.Description,
            Position = workItem.Position,
            Priority = workItem.Priority,
            DueDate = workItem.DueDate,
            StoryPoints = workItem.StoryPoints,
            IsArchived = workItem.IsArchived,
            CommentCount = commentCount,
            AttachmentCount = attachmentCount,
            Assignments = assignments,
            Labels = labels,
            ETag = workItem.ETag,
            CreatedAt = workItem.CreatedAt,
            UpdatedAt = workItem.UpdatedAt
        };
    }

    private static WorkItemDto MapToChildDto(WorkItem workItem)
    {
        return new WorkItemDto
        {
            Id = workItem.Id,
            ProductId = workItem.ProductId,
            ParentWorkItemId = workItem.ParentWorkItemId,
            Type = workItem.Type,
            SwimlaneId = workItem.SwimlaneId,
            SwimlaneTitle = workItem.Swimlane?.Title,
            ItemNumber = workItem.ItemNumber,
            Title = workItem.Title,
            Description = workItem.Description,
            Position = workItem.Position,
            Priority = workItem.Priority,
            DueDate = workItem.DueDate,
            StoryPoints = workItem.StoryPoints,
            IsArchived = workItem.IsArchived,
            ETag = workItem.ETag,
            CreatedAt = workItem.CreatedAt,
            UpdatedAt = workItem.UpdatedAt
        };
    }
}
