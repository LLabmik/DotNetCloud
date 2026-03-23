using DotNetCloud.Modules.Calendar.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Calendar.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EventAttendee"/> entity.
/// </summary>
public sealed class EventAttendeeConfiguration : IEntityTypeConfiguration<EventAttendee>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EventAttendee> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(a => a.DisplayName).HasMaxLength(200);

        builder.Property(a => a.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.Comment).HasMaxLength(1000);

        builder.Property(a => a.CreatedByUserId);
        builder.Property(a => a.UpdatedByUserId);

        // Relationships
        builder.HasOne(a => a.Event)
            .WithMany(e => e.Attendees)
            .HasForeignKey(a => a.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(a => a.EventId)
            .HasDatabaseName("ix_event_attendees_event_id");

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("ix_event_attendees_user_id");

        builder.HasIndex(a => new { a.EventId, a.Email })
            .IsUnique()
            .HasDatabaseName("ix_event_attendees_event_email");
    }
}
