using DotNetCloud.Modules.Calendar.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Calendar.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ReminderLog"/> entity.
/// </summary>
public sealed class ReminderLogConfiguration : IEntityTypeConfiguration<ReminderLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReminderLog> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.OccurrenceStartUtc).IsRequired();
        builder.Property(r => r.TriggeredAtUtc).IsRequired();
        builder.Property(r => r.ErrorMessage).HasMaxLength(2000);

        // Relationships
        builder.HasOne(r => r.Reminder)
            .WithMany()
            .HasForeignKey(r => r.ReminderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index: one log per reminder + occurrence
        builder.HasIndex(r => new { r.ReminderId, r.OccurrenceStartUtc })
            .IsUnique()
            .HasDatabaseName("ix_reminder_logs_reminder_occurrence");

        // Query performance: find pending reminders by event
        builder.HasIndex(r => r.EventId)
            .HasDatabaseName("ix_reminder_logs_event_id");
    }
}
