using DotNetCloud.Modules.Calendar.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Calendar.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CalendarShare"/> entity.
/// </summary>
public sealed class CalendarShareConfiguration : IEntityTypeConfiguration<CalendarShare>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CalendarShare> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Permission)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.CreatedByUserId);
        builder.Property(s => s.UpdatedByUserId);

        // Relationships
        builder.HasOne(s => s.Calendar)
            .WithMany(c => c.Shares)
            .HasForeignKey(s => s.CalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => s.CalendarId)
            .HasDatabaseName("ix_calendar_shares_calendar_id");

        builder.HasIndex(s => s.SharedWithUserId)
            .HasDatabaseName("ix_calendar_shares_user_id");

        builder.HasIndex(s => s.SharedWithTeamId)
            .HasDatabaseName("ix_calendar_shares_team_id");
    }
}
