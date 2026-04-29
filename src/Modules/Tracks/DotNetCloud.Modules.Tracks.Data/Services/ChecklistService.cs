using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class ChecklistService
{
    private const double PositionGap = 65536.0;

    private readonly TracksDbContext _db;

    public ChecklistService(TracksDbContext db)
    {
        _db = db;
    }

    public async Task<ChecklistDto> CreateChecklistAsync(
        Guid itemId,
        CreateChecklistDto dto,
        CancellationToken ct)
    {
        var maxPosition = await _db.Checklists
            .Where(c => c.ItemId == itemId)
            .MaxAsync(c => (double?)c.Position, ct) ?? 0;

        var checklist = new Checklist
        {
            ItemId = itemId,
            Title = dto.Title,
            Position = maxPosition + PositionGap,
            CreatedAt = DateTime.UtcNow
        };

        _db.Checklists.Add(checklist);
        await _db.SaveChangesAsync(ct);

        return Map(checklist);
    }

    public async Task<List<ChecklistDto>> GetChecklistsByItemAsync(
        Guid itemId,
        CancellationToken ct)
    {
        return await _db.Checklists
            .Where(c => c.ItemId == itemId)
            .Include(c => c.Items)
            .OrderBy(c => c.Position)
            .Select(c => Map(c))
            .ToListAsync(ct);
    }

    public async Task DeleteChecklistAsync(Guid checklistId, CancellationToken ct)
    {
        var checklist = await _db.Checklists.FindAsync(new object[] { checklistId }, ct);

        if (checklist is not null)
        {
            _db.Checklists.Remove(checklist);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<ChecklistItemDto> AddChecklistItemAsync(
        Guid checklistId,
        AddChecklistItemDto dto,
        CancellationToken ct)
    {
        var maxPosition = await _db.ChecklistItems
            .Where(i => i.ChecklistId == checklistId)
            .MaxAsync(i => (double?)i.Position, ct) ?? 0;

        var now = DateTime.UtcNow;
        var item = new ChecklistItem
        {
            ChecklistId = checklistId,
            Title = dto.Title,
            AssignedToUserId = dto.AssignedToUserId,
            Position = maxPosition + PositionGap,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ChecklistItems.Add(item);
        await _db.SaveChangesAsync(ct);

        return Map(item);
    }

    public async Task ToggleChecklistItemAsync(Guid itemId, CancellationToken ct)
    {
        var item = await _db.ChecklistItems.FindAsync(new object[] { itemId }, ct);

        if (item is null)
        {
            throw new InvalidOperationException("Checklist item not found.");
        }

        item.IsCompleted = !item.IsCompleted;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteChecklistItemAsync(Guid itemId, CancellationToken ct)
    {
        var item = await _db.ChecklistItems.FindAsync(new object[] { itemId }, ct);

        if (item is not null)
        {
            _db.ChecklistItems.Remove(item);
            await _db.SaveChangesAsync(ct);
        }
    }

    private static ChecklistDto Map(Checklist c) => new()
    {
        Id = c.Id,
        ItemId = c.ItemId,
        Title = c.Title,
        Position = c.Position,
        Items = c.Items
            .OrderBy(i => i.Position)
            .Select(Map)
            .ToList(),
        CreatedAt = c.CreatedAt
    };

    private static ChecklistItemDto Map(ChecklistItem i) => new()
    {
        Id = i.Id,
        ChecklistId = i.ChecklistId,
        Title = i.Title,
        IsCompleted = i.IsCompleted,
        Position = i.Position,
        AssignedToUserId = i.AssignedToUserId,
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt
    };
}
