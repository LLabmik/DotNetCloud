using System.Security.Cryptography;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Manages guest user invitations, acceptance, and per-work-item permissions.
/// </summary>
public sealed class GuestAccessService
{
    private readonly TracksDbContext _db;

    public GuestAccessService(TracksDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Invites a guest user by email to access a product.
    /// Returns the invite token for constructing the invitation link.
    /// </summary>
    public async Task<GuestUserDto> InviteGuestAsync(
        Guid productId,
        Guid invitedByUserId,
        InviteGuestDto dto,
        CancellationToken ct)
    {
        // Check if guest already exists for this product
        var existing = await _db.GuestUsers
            .FirstOrDefaultAsync(g => g.Email == dto.Email && g.ProductId == productId, ct);

        if (existing is not null)
        {
            if (existing.Status == GuestStatus.Revoked)
            {
                // Re-invite a previously revoked guest
                existing.Status = GuestStatus.Pending;
                existing.InviteToken = GenerateSecureToken();
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                return Map(existing);
            }

            throw new InvalidOperationException($"A guest with email '{dto.Email}' already exists for this product.");
        }

        var guest = new GuestUser
        {
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            ProductId = productId,
            InvitedByUserId = invitedByUserId,
            Status = GuestStatus.Pending,
            InviteToken = GenerateSecureToken(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.GuestUsers.Add(guest);
        await _db.SaveChangesAsync(ct);

        return Map(guest);
    }

    /// <summary>
    /// Accepts a guest invitation using the invite token.
    /// </summary>
    public async Task<GuestUserDto> AcceptInviteAsync(string inviteToken, CancellationToken ct)
    {
        var guest = await _db.GuestUsers
            .FirstOrDefaultAsync(g => g.InviteToken == inviteToken && g.Status == GuestStatus.Pending, ct);

        if (guest is null)
            throw new InvalidOperationException("Invalid or expired invitation token.");

        guest.Status = GuestStatus.Active;
        guest.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Map(guest);
    }

    /// <summary>
    /// Revokes a guest's access to a product.
    /// </summary>
    public async Task RevokeGuestAsync(Guid guestId, CancellationToken ct)
    {
        var guest = await _db.GuestUsers.FindAsync(new object[] { guestId }, ct);
        if (guest is null)
            throw new InvalidOperationException("Guest not found.");

        guest.Status = GuestStatus.Revoked;
        guest.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Lists all guests for a product.
    /// </summary>
    public async Task<List<GuestUserDto>> GetGuestsByProductAsync(Guid productId, CancellationToken ct)
    {
        return await _db.GuestUsers
            .AsNoTracking()
            .Where(g => g.ProductId == productId)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => Map(g))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Grants a guest permission to view or comment on a specific work item.
    /// </summary>
    public async Task GrantPermissionAsync(
        Guid guestUserId,
        Guid workItemId,
        GuestPermissionLevel permission,
        CancellationToken ct)
    {
        var existing = await _db.GuestPermissions
            .FirstOrDefaultAsync(p => p.GuestUserId == guestUserId && p.WorkItemId == workItemId, ct);

        if (existing is not null)
        {
            existing.Permission = permission;
        }
        else
        {
            _db.GuestPermissions.Add(new GuestPermission
            {
                GuestUserId = guestUserId,
                WorkItemId = workItemId,
                Permission = permission,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Revokes a guest's permission on a specific work item.
    /// </summary>
    public async Task RevokePermissionAsync(Guid guestUserId, Guid workItemId, CancellationToken ct)
    {
        var permission = await _db.GuestPermissions
            .FirstOrDefaultAsync(p => p.GuestUserId == guestUserId && p.WorkItemId == workItemId, ct);

        if (permission is not null)
        {
            _db.GuestPermissions.Remove(permission);
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Gets the effective permission level for a guest on a work item.
    /// Returns null if no access is granted.
    /// </summary>
    public async Task<GuestPermissionLevel?> GetEffectivePermissionAsync(
        Guid guestUserId,
        Guid workItemId,
        CancellationToken ct)
    {
        var permission = await _db.GuestPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.GuestUserId == guestUserId && p.WorkItemId == workItemId, ct);

        return permission?.Permission;
    }

    /// <summary>
    /// Lists all work items a guest has access to.
    /// </summary>
    public async Task<List<Guid>> GetAccessibleWorkItemIdsAsync(Guid guestUserId, CancellationToken ct)
    {
        return await _db.GuestPermissions
            .Where(p => p.GuestUserId == guestUserId)
            .Select(p => p.WorkItemId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Resolves a guest by invite token (for authentication).
    /// </summary>
    public async Task<GuestUserDto?> GetGuestByTokenAsync(string inviteToken, CancellationToken ct)
    {
        var guest = await _db.GuestUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.InviteToken == inviteToken && g.Status == GuestStatus.Active, ct);

        return guest is null ? null : Map(guest);
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

    private static GuestUserDto Map(GuestUser g) => new()
    {
        Id = g.Id,
        Email = g.Email,
        DisplayName = g.DisplayName,
        ProductId = g.ProductId,
        Status = g.Status switch
        {
            GuestStatus.Active => "active",
            GuestStatus.Revoked => "revoked",
            _ => "pending"
        },
        InvitedByUserId = g.InvitedByUserId,
        CreatedAt = g.CreatedAt
    };
}
