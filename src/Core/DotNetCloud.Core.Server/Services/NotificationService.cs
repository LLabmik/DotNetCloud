using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Notifications;
using DotNetCloud.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Database-backed implementation of <see cref="INotificationService"/>.
/// </summary>
internal sealed class NotificationService : INotificationService
{
    private readonly CoreDbContext _db;

    public NotificationService(CoreDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var entity = new Notification
        {
            Id = notification.Id,
            UserId = userId,
            SourceModuleId = notification.SourceModuleId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            Priority = notification.Priority,
            ActionUrl = notification.ActionUrl,
            RelatedEntityType = notification.RelatedEntityType,
            RelatedEntityId = notification.RelatedEntityId,
            CreatedAtUtc = notification.CreatedAtUtc,
            ReadAtUtc = notification.ReadAtUtc
        };

        _db.Notifications.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendToManyAsync(IEnumerable<Guid> userIds, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        ArgumentNullException.ThrowIfNull(notification);

        var now = DateTime.UtcNow;
        var entities = userIds
            .Distinct()
            .Select(userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SourceModuleId = notification.SourceModuleId,
                Type = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                Priority = notification.Priority,
                ActionUrl = notification.ActionUrl,
                RelatedEntityType = notification.RelatedEntityType,
                RelatedEntityId = notification.RelatedEntityId,
                CreatedAtUtc = notification.CreatedAtUtc == default ? now : notification.CreatedAtUtc,
                ReadAtUtc = notification.ReadAtUtc
            })
            .ToList();

        _db.Notifications.AddRange(entities);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NotificationDto>> GetUnreadAsync(Guid userId, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var notifications = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.ReadAtUtc == null)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(Math.Clamp(maxResults, 1, 200))
            .ToListAsync(cancellationToken);

        return notifications.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification is null)
        {
            return;
        }

        notification.ReadAtUtc ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unread = await _db.Notifications
            .Where(n => n.UserId == userId && n.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.ReadAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, cancellationToken);
    }

    private static NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            SourceModuleId = notification.SourceModuleId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            Priority = notification.Priority,
            ActionUrl = notification.ActionUrl,
            RelatedEntityType = notification.RelatedEntityType,
            RelatedEntityId = notification.RelatedEntityId,
            CreatedAtUtc = notification.CreatedAtUtc,
            ReadAtUtc = notification.ReadAtUtc
        };
    }
}
