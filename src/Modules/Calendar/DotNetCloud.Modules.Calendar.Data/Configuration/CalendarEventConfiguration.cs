using DotNetCloud.Modules.Calendar.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Calendar.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CalendarEvent"/> entity.
/// </summary>
public sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Description).HasMaxLength(10000);
        builder.Property(e => e.Location).HasMaxLength(500);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.RecurrenceRule).HasMaxLength(500);

        builder.Property(e => e.Color).HasMaxLength(20);
        builder.Property(e => e.Url).HasMaxLength(2000);

        builder.Property(e => e.ETag)
            .IsRequired()
            .HasMaxLength(64)
            .IsConcurrencyToken();

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedByUserId);

        // Soft-delete query filter
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Relationships
        builder.HasOne(e => e.Calendar)
            .WithMany(c => c.Events)
            .HasForeignKey(e => e.CalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.RecurringEvent)
            .WithMany(e => e.Exceptions)
            .HasForeignKey(e => e.RecurringEventId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.CalendarId)
            .HasDatabaseName("ix_calendar_events_calendar_id");

        builder.HasIndex(e => e.CreatedByUserId)
            .HasDatabaseName("ix_calendar_events_created_by");

        builder.HasIndex(e => new { e.CalendarId, e.StartUtc })
            .HasDatabaseName("ix_calendar_events_calendar_start");

        builder.HasIndex(e => new { e.StartUtc, e.EndUtc })
            .HasDatabaseName("ix_calendar_events_time_range");

        builder.HasIndex(e => e.RecurringEventId)
            .HasDatabaseName("ix_calendar_events_recurring_parent");

        builder.HasIndex(e => e.IsDeleted)
            .HasDatabaseName("ix_calendar_events_is_deleted");
    }
}
