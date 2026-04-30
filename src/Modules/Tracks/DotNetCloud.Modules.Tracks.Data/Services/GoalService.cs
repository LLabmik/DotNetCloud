using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Manages goals and key results (OKRs) with progress tracking.
/// </summary>
public sealed class GoalService
{
    private readonly TracksDbContext _db;
    private readonly ILogger<GoalService> _logger;

    public GoalService(TracksDbContext db, ILogger<GoalService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Lists all goals for a product, with hierarchy.</summary>
    public async Task<List<GoalDto>> ListAsync(Guid productId, CancellationToken ct)
    {
        var goals = await _db.Goals
            .Where(g => g.ProductId == productId)
            .Include(g => g.LinkedWorkItems)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(ct);

        // Get done swimlane IDs for auto-progress calculation
        var doneSwimlaneIds = await _db.Swimlanes
            .Where(s => s.ContainerType == SwimlaneContainerType.Product && s.ContainerId == productId && s.IsDone)
            .Select(s => s.Id)
            .ToListAsync(ct);

        return goals.Select(g => ToDto(g, doneSwimlaneIds)).ToList();
    }

    /// <summary>Gets a single goal with its key results.</summary>
    public async Task<GoalDto?> GetAsync(Guid goalId, CancellationToken ct)
    {
        var goal = await _db.Goals
            .Include(g => g.LinkedWorkItems)
            .FirstOrDefaultAsync(g => g.Id == goalId, ct);

        if (goal is null) return null;

        var doneSwimlaneIds = await _db.Swimlanes
            .Where(s => s.ContainerType == SwimlaneContainerType.Product && s.ContainerId == goal.ProductId && s.IsDone)
            .Select(s => s.Id)
            .ToListAsync(ct);

        return ToDto(goal, doneSwimlaneIds);
    }

    /// <summary>Creates a new goal.</summary>
    public async Task<GoalDto> CreateAsync(Guid productId, CreateGoalDto dto, Guid userId, CancellationToken ct)
    {
        // If it's a key result, validate parent exists
        if (dto.Type == "key_result" && dto.ParentGoalId.HasValue)
        {
            var parent = await _db.Goals.FindAsync([dto.ParentGoalId.Value], ct);
            if (parent is null || parent.ProductId != productId)
                throw new InvalidOperationException("Parent goal not found or belongs to a different product.");
        }

        var goal = new Goal
        {
            ProductId = productId,
            Title = dto.Title,
            Description = dto.Description,
            Type = dto.Type,
            ParentGoalId = dto.ParentGoalId,
            TargetValue = dto.TargetValue,
            ProgressType = dto.ProgressType,
            DueDate = dto.DueDate,
            CreatedByUserId = userId,
            Status = "not_started"
        };

        _db.Goals.Add(goal);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Created goal {GoalId}: {GoalTitle}", goal.Id, goal.Title);
        return ToDto(goal, []);
    }

    /// <summary>Updates a goal.</summary>
    public async Task<GoalDto?> UpdateAsync(Guid goalId, UpdateGoalDto dto, CancellationToken ct)
    {
        var goal = await _db.Goals.FindAsync([goalId], ct);
        if (goal is null) return null;

        if (dto.Title is not null) goal.Title = dto.Title;
        if (dto.Description is not null) goal.Description = dto.Description;
        if (dto.TargetValue.HasValue) goal.TargetValue = dto.TargetValue.Value;
        if (dto.CurrentValue.HasValue) goal.CurrentValue = dto.CurrentValue.Value;
        if (dto.ProgressType is not null) goal.ProgressType = dto.ProgressType;
        if (dto.Status is not null) goal.Status = dto.Status;
        if (dto.DueDate is not null) goal.DueDate = dto.DueDate;
        goal.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var doneSwimlaneIds = await _db.Swimlanes
            .Where(s => s.ContainerType == SwimlaneContainerType.Product && s.ContainerId == goal.ProductId && s.IsDone)
            .Select(s => s.Id)
            .ToListAsync(ct);

        return ToDto(goal, doneSwimlaneIds);
    }

    /// <summary>Deletes a goal.</summary>
    public async Task<bool> DeleteAsync(Guid goalId, CancellationToken ct)
    {
        var goal = await _db.Goals.FindAsync([goalId], ct);
        if (goal is null) return false;

        _db.Goals.Remove(goal);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Links a work item to a goal/key result.</summary>
    public async Task<bool> LinkWorkItemAsync(Guid goalId, Guid workItemId, CancellationToken ct)
    {
        var goal = await _db.Goals.FindAsync([goalId], ct);
        if (goal is null) return false;

        var existing = await _db.GoalWorkItems
            .FirstOrDefaultAsync(gwi => gwi.GoalId == goalId && gwi.WorkItemId == workItemId, ct);

        if (existing is not null) return true; // Already linked

        _db.GoalWorkItems.Add(new GoalWorkItem
        {
            GoalId = goalId,
            WorkItemId = workItemId
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Unlinks a work item from a goal.</summary>
    public async Task<bool> UnlinkWorkItemAsync(Guid goalId, Guid workItemId, CancellationToken ct)
    {
        var link = await _db.GoalWorkItems
            .FirstOrDefaultAsync(gwi => gwi.GoalId == goalId && gwi.WorkItemId == workItemId, ct);

        if (link is null) return false;

        _db.GoalWorkItems.Remove(link);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Computes the effective status based on progress percentage and due date.</summary>
    public static string ComputeStatus(double progressPercent, DateTime? dueDate)
    {
        if (progressPercent >= 100) return "completed";
        if (dueDate.HasValue && dueDate.Value < DateTime.UtcNow && progressPercent < 80) return "behind";
        if (progressPercent >= 80) return "on_track";
        if (progressPercent >= 50) return "at_risk";
        return "behind";
    }

    private GoalDto ToDto(Goal goal, List<Guid> doneSwimlaneIds)
    {
        int linkedCount = goal.LinkedWorkItems.Count;
        int completedCount = 0;
        double progressPercent = 0;

        if (goal.ProgressType == "automatic" && linkedCount > 0)
        {
            // For automatic progress, check which linked work items are in done swimlanes
            var linkedItemIds = goal.LinkedWorkItems.Select(l => l.WorkItemId).ToHashSet();
            completedCount = _db.WorkItems
                .Count(wi => linkedItemIds.Contains(wi.Id) && !wi.IsDeleted
                    && wi.SwimlaneId != null && doneSwimlaneIds.Contains(wi.SwimlaneId.Value));

            progressPercent = goal.TargetValue.HasValue && goal.TargetValue.Value > 0
                ? Math.Min(100.0, (completedCount / goal.TargetValue.Value) * 100.0)
                : linkedCount > 0 ? (double)completedCount / linkedCount * 100.0 : 0;
        }
        else if (goal.TargetValue.HasValue && goal.TargetValue.Value > 0)
        {
            progressPercent = Math.Min(100.0, ((goal.CurrentValue ?? 0) / goal.TargetValue.Value) * 100.0);
        }

        var status = goal.Status == "not_started" || goal.Status == "completed"
            ? ComputeStatus(progressPercent, goal.DueDate)
            : goal.Status;

        return new GoalDto
        {
            Id = goal.Id,
            ProductId = goal.ProductId,
            Title = goal.Title,
            Description = goal.Description,
            Type = goal.Type,
            ParentGoalId = goal.ParentGoalId,
            TargetValue = goal.TargetValue,
            CurrentValue = goal.CurrentValue,
            ProgressType = goal.ProgressType,
            Status = status,
            DueDate = goal.DueDate,
            CreatedByUserId = goal.CreatedByUserId,
            LinkedWorkItemCount = linkedCount,
            CompletedLinkedWorkItemCount = completedCount,
            ProgressPercent = Math.Round(progressPercent, 1),
            CreatedAt = goal.CreatedAt,
            UpdatedAt = goal.UpdatedAt
        };
    }
}
