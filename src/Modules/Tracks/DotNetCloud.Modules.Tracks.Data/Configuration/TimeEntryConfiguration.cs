using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="TimeEntry"/> entity.
/// </summary>
public sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.DurationMinutes)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(t => t.Card)
            .WithMany(c => c.TimeEntries)
            .HasForeignKey(t => t.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(t => t.CardId)
            .HasDatabaseName("ix_time_entries_card_id");

        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("ix_time_entries_user_id");

        builder.HasIndex(t => new { t.CardId, t.UserId })
            .HasDatabaseName("ix_time_entries_card_user");

        builder.HasIndex(t => t.StartTime)
            .HasDatabaseName("ix_time_entries_start_time")
            .HasFilter("\"StartTime\" IS NOT NULL");
    }
}
