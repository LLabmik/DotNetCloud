using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class SwimlaneService
{
    private readonly TracksDbContext _db;

    public SwimlaneService(TracksDbContext db)
    {
        _db = db;
    }

    public async Task<SwimlaneDto> CreateSwimlaneAsync(
        SwimlaneContainerType containerType, Guid containerId, CreateSwimlaneDto dto, CancellationToken ct)
    {
        var maxPosition = await _db.Swimlanes
            .Where(s => s.ContainerType == containerType && s.ContainerId == containerId)
            .MaxAsync(s => (double?)s.Position, ct) ?? 0;

        var position = maxPosition > 0 ? maxPosition + 1024 : 1000;

        var swimlane = new Swimlane
        {
            ContainerType = containerType,
            ContainerId = containerId,
            Title = dto.Title,
            Color = dto.Color,
            Position = position,
            CardLimit = dto.CardLimit,
            IsDone = dto.IsDone
        };

        _db.Swimlanes.Add(swimlane);

        await _db.SaveChangesAsync(ct);

        return MapToDto(swimlane, 0);
    }

    public async Task<List<SwimlaneDto>> GetSwimlanesAsync(
        SwimlaneContainerType containerType, Guid containerId, CancellationToken ct)
    {
        // Ensure swimlanes exist for WorkItem containers (epics/features).
        // This lazily creates them if they were never created at epic-creation time
        // (e.g., epics created before replication was added, or failed gRPC calls).
        if (containerType == SwimlaneContainerType.WorkItem)
        {
            await EnsureWorkItemSwimlanesExistAsync(containerId, ct);
        }

        var swimlanes = await _db.Swimlanes
            .Where(s => s.ContainerType == containerType
                     && s.ContainerId == containerId
                     && !s.IsArchived)
            .Include(s => s.WorkItems)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);

        return swimlanes.Select(s => MapToDto(s, s.WorkItems.Count(wi => !wi.IsArchived))).ToList();
    }

    public async Task<SwimlaneDto> UpdateSwimlaneAsync(Guid swimlaneId, UpdateSwimlaneDto dto, CancellationToken ct)
    {
        var swimlane = await _db.Swimlanes
            .Include(s => s.WorkItems)
            .FirstOrDefaultAsync(s => s.Id == swimlaneId, ct)
            ?? throw new InvalidOperationException($"Swimlane {swimlaneId} not found.");

        if (dto.Title is not null)
            swimlane.Title = dto.Title;
        if (dto.Color is not null)
            swimlane.Color = dto.Color;
        if (dto.IsDone.HasValue)
            swimlane.IsDone = dto.IsDone.Value;
        if (dto.CardLimit.HasValue)
            swimlane.CardLimit = dto.CardLimit.Value;

        swimlane.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapToDto(swimlane, swimlane.WorkItems.Count(wi => !wi.IsArchived));
    }

    public async Task DeleteSwimlaneAsync(Guid swimlaneId, CancellationToken ct)
    {
        var swimlane = await _db.Swimlanes
            .FirstOrDefaultAsync(s => s.Id == swimlaneId, ct)
            ?? throw new InvalidOperationException($"Swimlane {swimlaneId} not found.");

        swimlane.IsArchived = true;
        swimlane.UpdatedAt = DateTime.UtcNow;

        var items = await _db.WorkItems
            .Where(wi => wi.SwimlaneId == swimlaneId)
            .ToListAsync(ct);

        foreach (var item in items)
        {
            item.SwimlaneId = null;
            item.ETag = Guid.CreateVersion7().ToString("N");
            item.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<SwimlaneDto>> ReorderSwimlanesAsync(List<Guid> orderedIds, CancellationToken ct)
    {
        var swimlanes = await _db.Swimlanes
            .Where(s => orderedIds.Contains(s.Id) && !s.IsArchived)
            .Include(s => s.WorkItems)
            .ToListAsync(ct);

        var swimlaneMap = swimlanes.ToDictionary(s => s.Id);

        for (int i = 0; i < orderedIds.Count; i++)
        {
            if (swimlaneMap.TryGetValue(orderedIds[i], out var swimlane))
            {
                swimlane.Position = 1000 + (i * 1024);
                swimlane.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        var ordered = orderedIds
            .Where(id => swimlaneMap.ContainsKey(id))
            .Select(id => swimlaneMap[id])
            .ToList();

        return ordered.Select(s => MapToDto(s, s.WorkItems.Count(wi => !wi.IsArchived))).ToList();
    }

    /// <summary>
    /// Ensures a WorkItem (epic/feature) has swimlanes. If none exist, replicates
    /// them from the parent product's swimlanes. Falls back to 3 defaults if the
    /// product also has no swimlanes. Idempotent — does nothing if swimlanes exist.
    /// </summary>
    private async Task EnsureWorkItemSwimlanesExistAsync(Guid workItemId, CancellationToken ct)
    {
        // Fast path: swimlanes already exist for this work item
        var anyExist = await _db.Swimlanes
            .AnyAsync(s => s.ContainerType == SwimlaneContainerType.WorkItem
                        && s.ContainerId == workItemId
                        && !s.IsArchived, ct);
        if (anyExist)
            return;

        // Look up the work item to get its ProductId
        var workItem = await _db.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(wi => wi.Id == workItemId && !wi.IsDeleted, ct);

        if (workItem is null)
            return; // Work item not found — nothing to do

        // Only epics and features get their own swimlane boards
        if (workItem.Type is not WorkItemType.Epic and not WorkItemType.Feature)
            return;

        // Replicate product-level swimlanes (same logic as WorkItemService.CreateWorkItemAsync)
        var productSwimlanes = await _db.Swimlanes
            .Where(s => s.ContainerType == SwimlaneContainerType.Product
                     && s.ContainerId == workItem.ProductId
                     && !s.IsArchived)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);

        if (productSwimlanes.Count > 0)
        {
            var childSwimlanes = productSwimlanes.Select(s => new Swimlane
            {
                ContainerType = SwimlaneContainerType.WorkItem,
                ContainerId = workItemId,
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
            // Fallback: create 3 default swimlanes
            _db.Swimlanes.AddRange(
                new Swimlane { ContainerType = SwimlaneContainerType.WorkItem, ContainerId = workItemId, Title = "To Do", Position = 1000 },
                new Swimlane { ContainerType = SwimlaneContainerType.WorkItem, ContainerId = workItemId, Title = "In Progress", Position = 2000 },
                new Swimlane { ContainerType = SwimlaneContainerType.WorkItem, ContainerId = workItemId, Title = "Done", Position = 3000, IsDone = true }
            );
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gets a swimlane entity by ID (not DTO). Used internally for transition rule lookups.
    /// </summary>
    public async Task<Swimlane?> GetSwimlaneByIdAsync(Guid swimlaneId, CancellationToken ct)
    {
        return await _db.Swimlanes
            .FirstOrDefaultAsync(s => s.Id == swimlaneId && !s.IsArchived, ct);
    }

    private static SwimlaneDto MapToDto(Swimlane swimlane, int cardCount)
    {
        return new SwimlaneDto
        {
            Id = swimlane.Id,
            ContainerType = swimlane.ContainerType,
            ContainerId = swimlane.ContainerId,
            Title = swimlane.Title,
            Color = swimlane.Color,
            Position = swimlane.Position,
            CardLimit = swimlane.CardLimit,
            IsDone = swimlane.IsDone,
            IsArchived = swimlane.IsArchived,
            CardCount = cardCount,
            CreatedAt = swimlane.CreatedAt,
            UpdatedAt = swimlane.UpdatedAt
        };
    }
}
