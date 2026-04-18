using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Sprint"/> entity.
/// </summary>
public sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Goal)
            .HasColumnType("text");

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.DurationWeeks);

        builder.Property(s => s.PlannedOrder);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(s => s.Board)
            .WithMany(b => b.Sprints)
            .HasForeignKey(s => s.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => new { s.BoardId, s.Status })
            .HasDatabaseName("ix_sprints_board_status");

        builder.HasIndex(s => s.StartDate)
            .HasDatabaseName("ix_sprints_start_date")
            .HasFilter("\"StartDate\" IS NOT NULL");
    }
}
