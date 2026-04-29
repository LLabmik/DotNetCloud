using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class DependencyService
{
    private readonly TracksDbContext _db;

    public DependencyService(TracksDbContext db)
    {
        _db = db;
    }

    public async Task<WorkItemDependencyDto> AddDependencyAsync(
        Guid workItemId,
        AddWorkItemDependencyDto dto,
        CancellationToken ct)
    {
        if (workItemId == dto.DependsOnWorkItemId)
        {
            throw new InvalidOperationException("A work item cannot depend on itself.");
        }

        if (dto.Type == DependencyType.BlockedBy)
        {
            var cycleExists = await WouldCreateCycleAsync(workItemId, dto.DependsOnWorkItemId, ct);
            if (cycleExists)
            {
                throw new InvalidOperationException("Adding this dependency would create a circular chain of blocked-by relationships.");
            }
        }

        var dependency = new WorkItemDependency
        {
            WorkItemId = workItemId,
            DependsOnWorkItemId = dto.DependsOnWorkItemId,
            Type = dto.Type,
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkItemDependencies.Add(dependency);
        await _db.SaveChangesAsync(ct);

        // Reload to get navigation property for title
        var result = await _db.WorkItemDependencies
            .Include(d => d.DependsOnWorkItem)
            .FirstAsync(d => d.Id == dependency.Id, ct);

        return Map(result);
    }

    public async Task<List<WorkItemDependencyDto>> GetDependenciesByWorkItemAsync(
        Guid workItemId,
        CancellationToken ct)
    {
        return await _db.WorkItemDependencies
            .Where(d => d.WorkItemId == workItemId)
            .Include(d => d.DependsOnWorkItem)
            .OrderBy(d => d.CreatedAt)
            .Select(d => Map(d))
            .ToListAsync(ct);
    }

    public async Task<List<WorkItemDependencyDto>> GetDependentsByWorkItemAsync(
        Guid workItemId,
        CancellationToken ct)
    {
        return await _db.WorkItemDependencies
            .Where(d => d.DependsOnWorkItemId == workItemId)
            .Include(d => d.DependsOnWorkItem)
            .OrderBy(d => d.CreatedAt)
            .Select(d => Map(d))
            .ToListAsync(ct);
    }

    public async Task RemoveDependencyAsync(Guid dependencyId, CancellationToken ct)
    {
        var dependency = await _db.WorkItemDependencies.FindAsync(new object[] { dependencyId }, ct);

        if (dependency is not null)
        {
            _db.WorkItemDependencies.Remove(dependency);
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<bool> WouldCreateCycleAsync(Guid workItemId, Guid dependsOnId, CancellationToken ct)
    {
        // BFS from dependsOnId — if we can reach workItemId through existing BlockedBy
        // dependencies, then adding workItemId -> dependsOnId would form a cycle.
        var allBlockedBy = await _db.WorkItemDependencies
            .Where(d => d.Type == DependencyType.BlockedBy)
            .Select(d => new { d.WorkItemId, d.DependsOnWorkItemId })
            .ToListAsync(ct);

        // Build adjacency list: workItemId -> list of items it depends on
        var adjacency = new Dictionary<Guid, List<Guid>>();
        foreach (var dep in allBlockedBy)
        {
            if (!adjacency.TryGetValue(dep.WorkItemId, out var list))
            {
                list = new List<Guid>();
                adjacency[dep.WorkItemId] = list;
            }
            list.Add(dep.DependsOnWorkItemId);
        }

        // BFS
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(dependsOnId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == workItemId)
            {
                return true;
            }

            if (!visited.Add(current))
            {
                continue;
            }

            if (adjacency.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return false;
    }

    private static WorkItemDependencyDto Map(WorkItemDependency d) => new()
    {
        Id = d.Id,
        WorkItemId = d.WorkItemId,
        DependsOnWorkItemId = d.DependsOnWorkItemId,
        DependsOnTitle = d.DependsOnWorkItem?.Title,
        Type = d.Type,
        CreatedAt = d.CreatedAt
    };
}
