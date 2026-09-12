using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Admin;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Database-backed implementation of <see cref="IAdminBroadcastService"/>.
/// </summary>
/// <remarks>
/// Broadcasts are persisted so users who log in (or reconnect) after the message was
/// sent still see it until it expires or is deleted. Delivery targets the
/// <c>admin-broadcast</c> SignalR group, which only the Blazor circuit relay joins —
/// native clients (Android/desktop) are deliberately not part of that group.
/// </remarks>
internal sealed class AdminBroadcastService : IAdminBroadcastService
{
    /// <summary>SignalR group joined by the server-side Blazor circuit relays.</summary>
    internal const string BroadcastGroup = "admin-broadcast";

    /// <summary>SignalR event carrying a newly delivered broadcast.</summary>
    internal const string BroadcastEvent = "admin.broadcast";

    /// <summary>SignalR event telling open modals to close because the broadcast was deleted.</summary>
    internal const string BroadcastRemovedEvent = "admin.broadcast.removed";

    private readonly CoreDbContext _db;
    private readonly IRealtimeBroadcaster _broadcaster;
    private readonly ILogger<AdminBroadcastService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminBroadcastService"/> class.
    /// </summary>
    public AdminBroadcastService(
        CoreDbContext db,
        IRealtimeBroadcaster broadcaster,
        ILogger<AdminBroadcastService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AdminBroadcastDto> CreateAsync(
        CreateAdminBroadcastRequest request,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        var now = DateTime.UtcNow;
        var scheduledFor = NormalizeUtc(request.ScheduledForUtc);
        var expiresAt = NormalizeUtc(request.ExpiresAtUtc);

        var sendAt = scheduledFor is null || scheduledFor <= now ? now : scheduledFor.Value;
        if (expiresAt is not null && expiresAt <= sendAt)
        {
            throw new ArgumentException("Expiry must be later than the scheduled send time.", nameof(request));
        }

        var entity = new AdminBroadcast
        {
            Id = Guid.CreateVersion7(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            Severity = request.Severity,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            ScheduledForUtc = scheduledFor,
            ExpiresAtUtc = expiresAt,
            SentAtUtc = sendAt == now ? now : null,
        };

        _db.AdminBroadcasts.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        if (sendAt == now)
        {
            await PublishAsync(entity, cancellationToken);
        }

        _logger.LogInformation(
            "Admin broadcast {BroadcastId} created by {UserId} ({Severity}); {State}",
            entity.Id,
            createdByUserId,
            entity.Severity,
            entity.SentAtUtc.HasValue ? "delivered immediately" : $"scheduled for {scheduledFor:u}");

        return MapToDto(entity, dismissedCount: 0);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminBroadcastDto>> ListAsync(
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var broadcasts = await _db.AdminBroadcasts
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        if (broadcasts.Count == 0)
        {
            return [];
        }

        var ids = broadcasts.Select(b => b.Id).ToList();
        var dismissalCounts = await _db.AdminBroadcastDismissals
            .AsNoTracking()
            .Where(d => ids.Contains(d.BroadcastId))
            .GroupBy(d => d.BroadcastId)
            .Select(g => new { BroadcastId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BroadcastId, x => x.Count, cancellationToken);

        return [.. broadcasts.Select(b => MapToDto(b, dismissalCounts.GetValueOrDefault(b.Id)))];
    }

    /// <inheritdoc />
    public async Task<bool> SendNowAsync(Guid broadcastId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.AdminBroadcasts
            .FirstOrDefaultAsync(b => b.Id == broadcastId, cancellationToken);

        if (entity is null || entity.SentAtUtc.HasValue)
        {
            return false;
        }

        entity.SentAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await PublishAsync(entity, cancellationToken);

        _logger.LogInformation("Scheduled admin broadcast {BroadcastId} sent manually", entity.Id);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid broadcastId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.AdminBroadcasts
            .FirstOrDefaultAsync(b => b.Id == broadcastId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        // Dismissal records are meaningless once the broadcast is gone, so remove them
        // outright rather than leaving orphans behind. The FK cascade in the database
        // is a safety net; this keeps behaviour identical on the in-memory provider.
        if (_db.Database.IsRelational())
        {
            await _db.AdminBroadcastDismissals
                .Where(d => d.BroadcastId == broadcastId)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            var dismissals = await _db.AdminBroadcastDismissals
                .Where(d => d.BroadcastId == broadcastId)
                .ToListAsync(cancellationToken);
            _db.AdminBroadcastDismissals.RemoveRange(dismissals);
        }

        _db.AdminBroadcasts.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        // Close the modal wherever it is still open.
        await _broadcaster.BroadcastAsync(
            BroadcastGroup, BroadcastRemovedEvent, entity.Id, cancellationToken);

        _logger.LogInformation("Admin broadcast {BroadcastId} deleted", broadcastId);
        return true;
    }

    /// <inheritdoc />
    public async Task<ActiveAdminBroadcastDto?> GetActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var entity = await _db.AdminBroadcasts
            .AsNoTracking()
            .Where(b => b.SentAtUtc != null)
            .Where(b => b.ExpiresAtUtc == null || b.ExpiresAtUtc > now)
            .Where(b => !_db.AdminBroadcastDismissals.Any(d => d.BroadcastId == b.Id && d.UserId == userId))
            .OrderByDescending(b => b.SentAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : MapToActiveDto(entity);
    }

    /// <inheritdoc />
    public async Task DismissAsync(Guid broadcastId, Guid userId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.AdminBroadcasts
            .AnyAsync(b => b.Id == broadcastId, cancellationToken);

        if (!exists)
        {
            _logger.LogDebug("Ignoring dismissal for unknown broadcast {BroadcastId}", broadcastId);
            return;
        }

        var alreadyDismissed = await _db.AdminBroadcastDismissals
            .AnyAsync(d => d.BroadcastId == broadcastId && d.UserId == userId, cancellationToken);

        if (alreadyDismissed)
        {
            return;
        }

        _db.AdminBroadcastDismissals.Add(new AdminBroadcastDismissal
        {
            Id = Guid.CreateVersion7(),
            BroadcastId = broadcastId,
            UserId = userId,
            DismissedAtUtc = DateTime.UtcNow,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Concurrent dismissal (unique index on BroadcastId + UserId) — harmless.
            _logger.LogDebug(ex, "Concurrent dismissal for broadcast {BroadcastId} by {UserId}", broadcastId, userId);
        }
    }

    /// <inheritdoc />
    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var due = await _db.AdminBroadcasts
            .Where(b => b.SentAtUtc == null)
            .Where(b => b.ScheduledForUtc == null || b.ScheduledForUtc <= now)
            .OrderBy(b => b.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var entity in due)
        {
            entity.SentAtUtc = now;
        }

        if (due.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        foreach (var entity in due)
        {
            await PublishAsync(entity, cancellationToken);
        }

        return due.Count;
    }

    /// <summary>
    /// Sends a delivered broadcast to the Blazor circuit relays.
    /// </summary>
    private async Task PublishAsync(AdminBroadcast entity, CancellationToken cancellationToken)
    {
        await _broadcaster.BroadcastAsync(
            BroadcastGroup, BroadcastEvent, MapToActiveDto(entity), cancellationToken);

        _logger.LogInformation(
            "Admin broadcast {BroadcastId} delivered to the {Group} group",
            entity.Id,
            BroadcastGroup);
    }

    /// <summary>
    /// Treats provider-supplied timestamps without a kind as UTC.
    /// </summary>
    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        };
    }

    /// <summary>
    /// Derives the lifecycle status shown in the admin history list.
    /// </summary>
    private static AdminBroadcastStatus DeriveStatus(AdminBroadcast entity, DateTime utcNow)
    {
        if (entity.SentAtUtc is null)
        {
            return AdminBroadcastStatus.Scheduled;
        }

        return entity.ExpiresAtUtc is not null && entity.ExpiresAtUtc <= utcNow
            ? AdminBroadcastStatus.Expired
            : AdminBroadcastStatus.Sent;
    }

    private static AdminBroadcastDto MapToDto(AdminBroadcast entity, int dismissedCount) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        Message = entity.Message,
        Severity = entity.Severity,
        Status = DeriveStatus(entity, DateTime.UtcNow),
        CreatedByUserId = entity.CreatedByUserId,
        CreatedAtUtc = entity.CreatedAtUtc,
        ScheduledForUtc = entity.ScheduledForUtc,
        SentAtUtc = entity.SentAtUtc,
        ExpiresAtUtc = entity.ExpiresAtUtc,
        DismissedCount = dismissedCount,
    };

    private static ActiveAdminBroadcastDto MapToActiveDto(AdminBroadcast entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        Message = entity.Message,
        Severity = entity.Severity,
        SentAtUtc = entity.SentAtUtc ?? entity.CreatedAtUtc,
        ExpiresAtUtc = entity.ExpiresAtUtc,
    };
}
