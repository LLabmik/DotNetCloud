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
            item.ETag = Guid.NewGuid().ToString("N");
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
