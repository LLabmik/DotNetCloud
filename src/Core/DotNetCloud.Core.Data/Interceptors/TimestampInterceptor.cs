using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Entities.Permissions;

namespace DotNetCloud.Core.Data.Interceptors;

/// <summary>
/// Interceptor for automatically setting CreatedAt and UpdatedAt timestamps on entities.
/// </summary>
/// <remarks>
/// This interceptor automatically updates timestamp properties whenever an entity is being saved:
/// - CreatedAt: Set only on entity creation (Added state)
/// - UpdatedAt: Set on both creation and modification (Added or Modified state)
/// </remarks>
public class TimestampInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Called before SaveChanges is executed.
    /// </summary>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyTimestamps(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Called before SaveChangesAsync is executed.
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ApplyTimestamps(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Applies timestamp values to entities being saved.
    /// </summary>
    /// <param name="context">The database context</param>
    private static void ApplyTimestamps(DbContext? context)
    {
        if (context == null)
            return;

        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                SetCreatedAtTimestamp(entry, now);
                SetUpdatedAtTimestamp(entry, now);
            }
            else if (entry.State == EntityState.Modified)
            {
                SetUpdatedAtTimestamp(entry, now);
            }
        }
    }

    /// <summary>
    /// Sets the CreatedAt timestamp property if the entity has one.
    /// </summary>
    private static void SetCreatedAtTimestamp(EntityEntry entry, DateTime timestamp)
    {
        var createdAtProperty = entry.Entity.GetType().GetProperty("CreatedAt");
        if (createdAtProperty != null && createdAtProperty.CanWrite)
        {
            var currentValue = createdAtProperty.GetValue(entry.Entity);
            if (currentValue == null || currentValue.Equals(default(DateTime)))
            {
                createdAtProperty.SetValue(entry.Entity, timestamp);
            }
        }
    }

    /// <summary>
    /// Sets the UpdatedAt timestamp property if the entity has one.
    /// </summary>
    private static void SetUpdatedAtTimestamp(EntityEntry entry, DateTime timestamp)
    {
        var updatedAtProperty = entry.Entity.GetType().GetProperty("UpdatedAt");
        if (updatedAtProperty != null && updatedAtProperty.CanWrite)
        {
            updatedAtProperty.SetValue(entry.Entity, timestamp);
        }
    }
}
