using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Manages product milestones — key date markers with progress tracking.
/// </summary>
public sealed class MilestoneService
{
    private readonly TracksDbContext _db;

    public MilestoneService(TracksDbContext db) => _db = db;

    public async Task<MilestoneDto> CreateMilestoneAsync(
        Guid productId, CreateMilestoneDto dto, CancellationToken ct)
    {
        var milestone = new Milestone
        {
            ProductId = productId,
            Title = dto.Title,
            Description = dto.Description,
            DueDate = dto.DueDate,
            Color = dto.Color,
            Status = MilestoneStatus.Upcoming
        };

        _db.Set<Milestone>().Add(milestone);
        await _db.SaveChangesAsync(ct);

        return MapToDto(milestone, 0, 0);
    }

    public async Task<List<MilestoneDto>> GetMilestonesAsync(Guid productId, CancellationToken ct)
    {
        var milestones = await _db.Set<Milestone>()
            .Where(m => m.ProductId == productId)
            .Include(m => m.WorkItems)
            .OrderBy(m => m.DueDate)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync(ct);

        return milestones.Select(m => MapToDto(m,
            m.WorkItems.Count(wi => !wi.IsArchived),
            m.WorkItems.Count(wi => wi.Swimlane != null && wi.Swimlane.IsDone && !wi.IsArchived))).ToList();
    }

    public async Task<MilestoneDto?> GetMilestoneAsync(Guid milestoneId, CancellationToken ct)
    {
        var milestone = await _db.Set<Milestone>()
            .Include(m => m.WorkItems)
            .FirstOrDefaultAsync(m => m.Id == milestoneId, ct);

        return milestone is null ? null : MapToDto(milestone,
            milestone.WorkItems.Count(wi => !wi.IsArchived),
            milestone.WorkItems.Count(wi => wi.Swimlane != null && wi.Swimlane.IsDone && !wi.IsArchived));
    }

    public async Task<MilestoneDto> UpdateMilestoneAsync(
        Guid milestoneId, UpdateMilestoneDto dto, CancellationToken ct)
    {
        var milestone = await _db.Set<Milestone>()
            .Include(m => m.WorkItems)
            .FirstOrDefaultAsync(m => m.Id == milestoneId, ct)
            ?? throw new NotFoundException("Milestone", milestoneId);

        if (dto.Title is not null) milestone.Title = dto.Title;
        if (dto.Description is not null) milestone.Description = dto.Description;
        if (dto.DueDate is not null) milestone.DueDate = dto.DueDate.Value;
        if (dto.Color is not null) milestone.Color = dto.Color;
        milestone.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapToDto(milestone,
            milestone.WorkItems.Count(wi => !wi.IsArchived),
            milestone.WorkItems.Count(wi => wi.Swimlane != null && wi.Swimlane.IsDone && !wi.IsArchived));
    }

    public async Task<MilestoneDto> SetStatusAsync(
        Guid milestoneId, SetMilestoneStatusDto dto, CancellationToken ct)
    {
        var milestone = await _db.Set<Milestone>()
            .Include(m => m.WorkItems)
            .FirstOrDefaultAsync(m => m.Id == milestoneId, ct)
            ?? throw new NotFoundException("Milestone", milestoneId);

        milestone.Status = (MilestoneStatus)dto.Status;
        milestone.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapToDto(milestone,
            milestone.WorkItems.Count(wi => !wi.IsArchived),
            milestone.WorkItems.Count(wi => wi.Swimlane != null && wi.Swimlane.IsDone && !wi.IsArchived));
    }

    public async Task DeleteMilestoneAsync(Guid milestoneId, CancellationToken ct)
    {
        var milestone = await _db.Set<Milestone>()
            .FirstOrDefaultAsync(m => m.Id == milestoneId, ct)
            ?? throw new NotFoundException("Milestone", milestoneId);

        // Unlink work items
        var linkedItems = await _db.Set<WorkItem>()
            .Where(wi => wi.MilestoneId == milestoneId)
            .ToListAsync(ct);

        foreach (var item in linkedItems)
        {
            item.MilestoneId = null;
            item.ETag = Guid.NewGuid().ToString("N");
            item.UpdatedAt = DateTime.UtcNow;
        }

        _db.Set<Milestone>().Remove(milestone);
        await _db.SaveChangesAsync(ct);
    }

    private static MilestoneDto MapToDto(Milestone m, int workItemCount, int completedCount) => new()
    {
        Id = m.Id,
        ProductId = m.ProductId,
        Title = m.Title,
        Description = m.Description,
        DueDate = m.DueDate,
        Status = (MilestoneStatus)m.Status,
        Color = m.Color,
        WorkItemCount = workItemCount,
        CompletedWorkItemCount = completedCount,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
