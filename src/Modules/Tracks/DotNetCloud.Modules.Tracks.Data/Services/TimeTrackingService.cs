using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class TimeTrackingService
{
    private readonly TracksDbContext _db;

    public TimeTrackingService(TracksDbContext db) => _db = db;

    public async Task<List<TimeEntryDto>> GetTimeEntriesByWorkItemAsync(Guid workItemId, CancellationToken ct)
    {
        var entries = await _db.TimeEntries
            .Where(t => t.WorkItemId == workItemId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return entries.Select(MapToDto).ToList();
    }

    public async Task<List<TimeEntryDto>> GetTimeEntriesByUserAsync(Guid userId, CancellationToken ct)
    {
        var entries = await _db.TimeEntries
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return entries.Select(MapToDto).ToList();
    }

    public async Task<TimeEntryDto> AddManualEntryAsync(Guid workItemId, Guid userId, CreateTimeEntryDto dto, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == workItemId && !wi.IsDeleted, ct)
            ?? throw new NotFoundException("WorkItem", workItemId);

        if (dto.DurationMinutes <= 0)
            throw new ValidationException("DurationMinutes", "Duration must be greater than zero.");

        var entry = new TimeEntry
        {
            WorkItemId = workItemId,
            UserId = userId,
            StartTime = dto.StartTime,
            EndTime = dto.StartTime?.AddMinutes(dto.DurationMinutes),
            DurationMinutes = dto.DurationMinutes,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        return MapToDto(entry);
    }

    public async Task DeleteEntryAsync(Guid entryId, CancellationToken ct)
    {
        var entry = await _db.TimeEntries.FindAsync([entryId], ct)
            ?? throw new NotFoundException("TimeEntry", entryId);

        _db.TimeEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TimeEntryDto> StartTimerAsync(Guid workItemId, Guid userId, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.Id == workItemId && !wi.IsDeleted, ct)
            ?? throw new NotFoundException("WorkItem", workItemId);

        var activeTimer = await _db.TimeEntries
            .FirstOrDefaultAsync(t => t.UserId == userId && t.StartTime != null && t.EndTime == null, ct);

        if (activeTimer is not null)
            throw new System.InvalidOperationException("User already has an active timer. Stop the existing timer before starting a new one.");

        var entry = new TimeEntry
        {
            WorkItemId = workItemId,
            UserId = userId,
            StartTime = DateTime.UtcNow,
            EndTime = null,
            DurationMinutes = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        return MapToDto(entry);
    }

    public async Task<TimeEntryDto> StopTimerAsync(Guid workItemId, Guid userId, CancellationToken ct)
    {
        var activeTimer = await _db.TimeEntries
            .FirstOrDefaultAsync(t => t.WorkItemId == workItemId && t.UserId == userId && t.StartTime != null && t.EndTime == null, ct)
            ?? throw new NotFoundException("No active timer found for this work item and user.");

        var endTime = DateTime.UtcNow;
        activeTimer.EndTime = endTime;

        if (activeTimer.StartTime is not null)
        {
            activeTimer.DurationMinutes = (int)Math.Ceiling((endTime - activeTimer.StartTime.Value).TotalMinutes);
        }

        activeTimer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return MapToDto(activeTimer);
    }

    public async Task<TimeEntryDto?> GetActiveTimerAsync(Guid userId, CancellationToken ct)
    {
        var activeTimer = await _db.TimeEntries
            .FirstOrDefaultAsync(t => t.UserId == userId && t.StartTime != null && t.EndTime == null, ct);

        return activeTimer is null ? null : MapToDto(activeTimer);
    }

    public async Task<int> GetTotalMinutesForWorkItemAsync(Guid workItemId, CancellationToken ct)
    {
        var total = await _db.TimeEntries
            .Where(t => t.WorkItemId == workItemId)
            .SumAsync(t => t.DurationMinutes, ct);

        return total;
    }

    private static TimeEntryDto MapToDto(TimeEntry entry)
    {
        return new TimeEntryDto
        {
            Id = entry.Id,
            WorkItemId = entry.WorkItemId,
            UserId = entry.UserId,
            StartTime = entry.StartTime,
            EndTime = entry.EndTime,
            DurationMinutes = entry.DurationMinutes,
            Description = entry.Description,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
    }
}
