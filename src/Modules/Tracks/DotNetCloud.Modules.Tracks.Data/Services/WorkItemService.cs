using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using IAuditLogger = DotNetCloud.Core.Capabilities.IAuditLogger;
using AuditEntry = DotNetCloud.Core.Capabilities.AuditEntry;
using AuditAction = DotNetCloud.Core.Capabilities.AuditAction;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class WorkItemService
{
    private readonly TracksDbContext _db;
    private readonly SwimlaneTransitionService _transitionService;
    private readonly IEventBus _eventBus;
    private readonly ActivityService _activityService;
    private readonly IAuditLogger _auditLogger;

    public WorkItemService(TracksDbContext db, SwimlaneTransitionService transitionService, IEventBus eventBus, ActivityService activityService, IAuditLogger auditLogger)
    {
        _db = db;
        _transitionService = transitionService;
        _eventBus = eventBus;
        _activityService = activityService;
        _auditLogger = auditLogger;
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

        // Replicate product-level swimlanes when an Epic or Feature is created,
        // so child work items have the same swimlane structure as the product.
        if (type is WorkItemType.Epic or WorkItemType.Feature)
        {
            var productSwimlanes = await _db.Swimlanes
                .Where(s => s.ContainerType == SwimlaneContainerType.Product
                         && s.ContainerId == productId
                         && !s.IsArchived)
                .OrderBy(s => s.Position)
                .ToListAsync(ct);

            if (productSwimlanes.Count > 0)
            {
                var childSwimlanes = productSwimlanes.Select(s => new Swimlane
                {
                    ContainerType = SwimlaneContainerType.WorkItem,
                    ContainerId = workItem.Id,
                    Title = s.Title,
                    Color = s.Color,
                    Position = s.Position,
                    CardLimit = s.CardLimit,
                    IsDone = s.IsDone
                }).ToList();

                _db.Swimlanes.AddRange(childSwimlanes);
            }
            else
            {
                // Fallback: create 3 default swimlanes if product has none
                _db.Swimlanes.AddRange(
                    new Swimlane { ContainerType = SwimlaneContainerType.WorkItem, ContainerId = workItem.Id, Title = "To Do", Position = 1000 },
                    new Swimlane { ContainerType = SwimlaneContainerType.WorkItem, ContainerId = workItem.Id, Title = "In Progress", Position = 2000 },
                    new Swimlane { ContainerType = SwimlaneContainerType.WorkItem, ContainerId = workItem.Id, Title = "Done", Position = 3000, IsDone = true }
                );
            }
        }

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
        if (dto.StartDate is not null)
            workItem.StartDate = dto.StartDate;
        if (dto.DueDate is not null)
            workItem.DueDate = dto.DueDate;
        if (dto.StoryPoints.HasValue)
            workItem.StoryPoints = dto.StoryPoints.Value;
        if (dto.IsArchived.HasValue)
            workItem.IsArchived = dto.IsArchived.Value;
        if (dto.MilestoneId is not null)
            workItem.MilestoneId = dto.MilestoneId;

        workItem.ETag = Guid.CreateVersion7().ToString("N");
        workItem.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetWorkItemAsync(workItemId, ct);
    }

    public async Task DeleteWorkItemAsync(Guid workItemId, Guid deletedByUserId, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == workItemId, ct)
            ?? throw new InvalidOperationException($"Work item {workItemId} not found.");

        workItem.IsDeleted = true;
        workItem.DeletedAt = DateTime.UtcNow;
        workItem.DeletedByUserId = deletedByUserId;
        workItem.ETag = Guid.CreateVersion7().ToString("N");
        workItem.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Record activity
        await _activityService.WriteWorkItemDeletedActivityAsync(workItem.ProductId, deletedByUserId, workItemId, workItem.Title, ct);

        // Publish deletion event
        var caller = new CallerContext(deletedByUserId, Array.Empty<string>(), CallerType.User);

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = caller,
            ModuleId = "dotnetcloud.tracks",
            Action = AuditAction.Delete,
            EntityType = "WorkItem",
            EntityId = workItemId,
            Description = "delete-workitem",
        }, ct);

        await _eventBus.PublishAsync(new WorkItemDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = workItemId,
            Type = workItem.Type
        }, caller, ct);
    }

    /// <summary>
    /// Permanently deletes a work item and all its child data.
    /// </summary>
    public async Task HardDeleteWorkItemAsync(Guid workItemId, CancellationToken ct)
    {
        // Collect all child work item IDs for cascade
        var allIds = new List<Guid> { workItemId };
        var childIds = await _db.WorkItems
            .IgnoreQueryFilters()
            .Where(wi => wi.ParentWorkItemId == workItemId)
            .Select(wi => wi.Id)
            .ToListAsync(ct);
        allIds.AddRange(childIds);

        // Collect sprint IDs for work items that are epics
        var sprintIds = await _db.Sprints
            .Where(s => allIds.Contains(s.EpicId))
            .Select(s => s.Id)
            .ToListAsync(ct);

        // Collect swimlane IDs for work-item-level swimlanes
        var swimlaneIds = await _db.Swimlanes
            .Where(s => s.ContainerType == SwimlaneContainerType.WorkItem && allIds.Contains(s.ContainerId))
            .Select(s => s.Id)
            .ToListAsync(ct);

        // Delete in reverse-dependency order

        // PokerVotes → PokerSessions
        var pokerSessionIds = await _db.PokerSessions
            .Where(ps => allIds.Contains(ps.EpicId))
            .Select(ps => ps.Id)
            .ToListAsync(ct);
        if (pokerSessionIds.Count > 0)
        {
            await _db.PokerVotes
                .Where(pv => pokerSessionIds.Contains(pv.SessionId))
                .ExecuteDeleteAsync(ct);
            await _db.PokerSessions
                .Where(ps => pokerSessionIds.Contains(ps.Id))
                .ExecuteDeleteAsync(ct);
        }

        // ReviewSessions
        await _db.Set<ReviewSession>()
            .Where(rs => allIds.Contains(rs.EpicId))
            .ExecuteDeleteAsync(ct);

        // SprintItems → Sprints
        if (sprintIds.Count > 0)
        {
            await _db.SprintItems
                .Where(si => sprintIds.Contains(si.SprintId))
                .ExecuteDeleteAsync(ct);
            await _db.Sprints
                .Where(s => sprintIds.Contains(s.Id))
                .ExecuteDeleteAsync(ct);
        }

        // ChecklistItems → Checklists
        var checklistIds = await _db.Checklists
            .Where(c => allIds.Contains(c.ItemId))
            .Select(c => c.Id)
            .ToListAsync(ct);
        if (checklistIds.Count > 0)
        {
            await _db.Set<ChecklistItem>()
                .Where(ci => checklistIds.Contains(ci.ChecklistId))
                .ExecuteDeleteAsync(ct);
            await _db.Checklists
                .Where(c => checklistIds.Contains(c.Id))
                .ExecuteDeleteAsync(ct);
        }

        // WorkItemDependencies (both directions)
        await _db.WorkItemDependencies
            .Where(d => allIds.Contains(d.WorkItemId) || allIds.Contains(d.DependsOnWorkItemId))
            .ExecuteDeleteAsync(ct);

        // WorkItemAttachments, Comments, Labels, Assignments, TimeEntries, Watchers, FieldValues
        await _db.WorkItemAttachments
            .Where(a => allIds.Contains(a.WorkItemId))
            .ExecuteDeleteAsync(ct);
        await _db.WorkItemComments
            .Where(c => allIds.Contains(c.WorkItemId))
            .ExecuteDeleteAsync(ct);
        await _db.WorkItemLabels
            .Where(wl => allIds.Contains(wl.WorkItemId))
            .ExecuteDeleteAsync(ct);
        await _db.WorkItemAssignments
            .Where(a => allIds.Contains(a.WorkItemId))
            .ExecuteDeleteAsync(ct);
        await _db.TimeEntries
            .Where(te => allIds.Contains(te.WorkItemId))
            .ExecuteDeleteAsync(ct);
        await _db.WorkItemWatchers
            .Where(ww => allIds.Contains(ww.WorkItemId))
            .ExecuteDeleteAsync(ct);
        await _db.WorkItemFieldValues
            .Where(fv => allIds.Contains(fv.WorkItemId))
            .ExecuteDeleteAsync(ct);

        // Work items themselves (children first, then parent)
        if (childIds.Count > 0)
        {
            await _db.WorkItems
                .IgnoreQueryFilters()
                .Where(wi => childIds.Contains(wi.Id))
                .ExecuteDeleteAsync(ct);
        }
        await _db.WorkItems
            .IgnoreQueryFilters()
            .Where(wi => wi.Id == workItemId)
            .ExecuteDeleteAsync(ct);

        // Swimlanes
        if (swimlaneIds.Count > 0)
        {
            await _db.Swimlanes
                .Where(s => swimlaneIds.Contains(s.Id))
                .ExecuteDeleteAsync(ct);
        }
    }

    /// <summary>
    /// Lists soft-deleted work items for a product.
    /// </summary>
    public async Task<List<WorkItemDto>> ListDeletedWorkItemsAsync(Guid productId, CancellationToken ct)
    {
        var items = await _db.WorkItems
            .IgnoreQueryFilters()
            .Where(wi => wi.ProductId == productId && wi.IsDeleted)
            .OrderByDescending(wi => wi.DeletedAt)
            .ToListAsync(ct);

        return items.Select(MapToDeletedDto).ToList();
    }

    /// <summary>
    /// Restores a soft-deleted work item.
    /// </summary>
    public async Task<WorkItemDto> RestoreWorkItemAsync(Guid workItemId, Guid restoredByUserId, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(wi => wi.Id == workItemId && wi.IsDeleted, ct)
            ?? throw new InvalidOperationException($"Deleted work item {workItemId} not found.");

        workItem.IsDeleted = false;
        workItem.DeletedAt = null;
        workItem.DeletedByUserId = null;
        workItem.ETag = Guid.CreateVersion7().ToString("N");
        workItem.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Record activity
        await _activityService.WriteWorkItemRestoredActivityAsync(workItem.ProductId, restoredByUserId, workItemId, workItem.Title, ct);

        // Publish update event for restore
        var caller = new CallerContext(restoredByUserId, Array.Empty<string>(), CallerType.User);
        await _eventBus.PublishAsync(new WorkItemUpdatedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = workItemId,
            Type = workItem.Type
        }, caller, ct);

        return await GetWorkItemAsync(workItemId, ct);
    }

    public async Task<WorkItemDto> MoveWorkItemAsync(Guid workItemId, MoveWorkItemDto dto, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .Include(wi => wi.Swimlane)
            .FirstOrDefaultAsync(wi => wi.Id == workItemId, ct)
            ?? throw new InvalidOperationException($"Work item {workItemId} not found.");

        var targetSwimlane = await _db.Swimlanes
            .FirstOrDefaultAsync(s => s.Id == dto.TargetSwimlaneId && !s.IsArchived, ct)
            ?? throw new InvalidOperationException($"Target swimlane {dto.TargetSwimlaneId} not found or is archived.");

        // ── Transition Rule Check ──
        if (workItem.SwimlaneId.HasValue && workItem.SwimlaneId.Value != dto.TargetSwimlaneId)
        {
            (bool isAllowed, List<Guid> allowedTargetIds) = await _transitionService.ValidateTransitionAsync(
                workItem.ProductId, workItem.SwimlaneId.Value, dto.TargetSwimlaneId, ct);

            if (!isAllowed)
            {
                var allowedNames = new List<string>();
                if (allowedTargetIds.Count > 0)
                {
                    var allowedSwimlanes = await _db.Swimlanes
                        .Where(s => allowedTargetIds.Contains(s.Id))
                        .Select(s => s.Title)
                        .ToListAsync(ct);
                    allowedNames.AddRange(allowedSwimlanes);
                }

                var allowedList = allowedNames.Count > 0
                    ? string.Join(", ", allowedNames)
                    : "none";

                throw new InvalidOperationException(
                    $"Cannot move from '{workItem.Swimlane?.Title ?? "unknown"}' to '{targetSwimlane.Title}'. " +
                    $"Allowed transitions: {allowedList}.");
            }
        }

        // ── WIP Limit Check ──
        if (targetSwimlane.CardLimit.HasValue && targetSwimlane.CardLimit.Value > 0)
        {
            var currentCount = await _db.WorkItems
                .CountAsync(wi => wi.SwimlaneId == dto.TargetSwimlaneId
                               && wi.Id != workItemId
                               && !wi.IsArchived, ct);

            if (currentCount >= targetSwimlane.CardLimit.Value)
            {
                if (dto.EnforceWipLimit == true)
                {
                    throw new InvalidOperationException(
                        $"Cannot move to '{targetSwimlane.Title}'. " +
                        $"WIP limit of {targetSwimlane.CardLimit.Value} has been reached.");
                }
                // Soft enforcement: allow the move but the caller should warn
            }
        }

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

        workItem.ETag = Guid.CreateVersion7().ToString("N");
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

    private static WorkItemDto MapToDeletedDto(WorkItem workItem)
    {
        return new WorkItemDto
        {
            Id = workItem.Id,
            ProductId = workItem.ProductId,
            ParentWorkItemId = workItem.ParentWorkItemId,
            Type = workItem.Type,
            SwimlaneId = workItem.SwimlaneId,
            ItemNumber = workItem.ItemNumber,
            Title = workItem.Title,
            Description = workItem.Description,
            Position = workItem.Position,
            Priority = workItem.Priority,
            DueDate = workItem.DueDate,
            StoryPoints = workItem.StoryPoints,
            IsArchived = workItem.IsArchived,
            CommentCount = 0,
            AttachmentCount = 0,
            ETag = workItem.ETag,
            CreatedAt = workItem.CreatedAt,
            UpdatedAt = workItem.UpdatedAt,
            DeletedAt = workItem.DeletedAt,
            DeletedByUserId = workItem.DeletedByUserId
        };
    }
}
