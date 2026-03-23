using DotNetCloud.Modules.Calendar.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Calendar.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EventReminder"/> entity.
/// </summary>
public sealed class EventReminderConfiguration : IEntityTypeConfiguration<EventReminder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EventReminder> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Method)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.CreatedByUserId);
        builder.Property(r => r.UpdatedByUserId);

        // Relationships
        builder.HasOne(r => r.Event)
            .WithMany(e => e.Reminders)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(r => r.EventId)
            .HasDatabaseName("ix_event_reminders_event_id");
    }
}
