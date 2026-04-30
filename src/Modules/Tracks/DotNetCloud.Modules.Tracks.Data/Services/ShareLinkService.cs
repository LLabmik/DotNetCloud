using System.Security.Cryptography;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Manages shareable links for work items — generation, validation, and revocation.
/// </summary>
public sealed class ShareLinkService
{
    private readonly TracksDbContext _db;

    public ShareLinkService(TracksDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Generates a new share link for a work item.
    /// </summary>
    public async Task<WorkItemShareLinkDto> GenerateShareLinkAsync(
        Guid workItemId,
        Guid createdByUserId,
        CreateShareLinkDto dto,
        CancellationToken ct)
    {
        // Verify work item exists
        var workItemExists = await _db.WorkItems
            .IgnoreQueryFilters()
            .AnyAsync(wi => wi.Id == workItemId && !wi.IsDeleted, ct);

        if (!workItemExists)
            throw new InvalidOperationException("Work item not found.");

        var token = GenerateSecureToken();

        var link = new WorkItemShareLink
        {
            WorkItemId = workItemId,
            CreatedByUserId = createdByUserId,
            Token = token,
            Permission = dto.Permission switch
            {
                "comment" => SharePermission.Comment,
                _ => SharePermission.View
            },
            ExpiresAt = dto.ExpiresInDays.HasValue
                ? DateTime.UtcNow.AddDays(dto.ExpiresInDays.Value)
                : null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkItemShareLinks.Add(link);
        await _db.SaveChangesAsync(ct);

        return Map(link);
    }

    /// <summary>
    /// Validates a share link token and returns the link if valid and active.
    /// </summary>
    public async Task<WorkItemShareLinkDto?> ValidateTokenAsync(string token, CancellationToken ct)
    {
        var link = await _db.WorkItemShareLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Token == token && l.IsActive, ct);

        if (link is null)
            return null;

        // Check expiry
        if (link.ExpiresAt.HasValue && link.ExpiresAt.Value < DateTime.UtcNow)
        {
            // Auto-deactivate expired links
            await RevokeShareLinkAsync(link.Id, ct);
            return null;
        }

        return Map(link);
    }

    /// <summary>
    /// Lists all active share links for a work item.
    /// </summary>
    public async Task<List<WorkItemShareLinkDto>> GetShareLinksByWorkItemAsync(
        Guid workItemId,
        CancellationToken ct)
    {
        return await _db.WorkItemShareLinks
            .AsNoTracking()
            .Where(l => l.WorkItemId == workItemId && l.IsActive)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => Map(l))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Revokes a share link by id.
    /// </summary>
    public async Task RevokeShareLinkAsync(Guid linkId, CancellationToken ct)
    {
        var link = await _db.WorkItemShareLinks.FindAsync(new object[] { linkId }, ct);
        if (link is null)
            throw new InvalidOperationException("Share link not found.");

        link.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Revokes all share links for a work item.
    /// </summary>
    public async Task RevokeAllShareLinksAsync(Guid workItemId, CancellationToken ct)
    {
        var links = await _db.WorkItemShareLinks
            .Where(l => l.WorkItemId == workItemId && l.IsActive)
            .ToListAsync(ct);

        foreach (var link in links)
        {
            link.IsActive = false;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static WorkItemShareLinkDto Map(WorkItemShareLink l) => new()
    {
        Id = l.Id,
        WorkItemId = l.WorkItemId,
        Token = l.Token,
        Permission = l.Permission == SharePermission.Comment ? "comment" : "view",
        ExpiresAt = l.ExpiresAt,
        IsActive = l.IsActive,
        CreatedAt = l.CreatedAt
    };
}
