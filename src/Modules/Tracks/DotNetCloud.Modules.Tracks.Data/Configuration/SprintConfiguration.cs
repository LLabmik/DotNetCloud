using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
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

        builder.HasOne(s => s.Epic)
            .WithMany()
            .HasForeignKey(s => s.EpicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.EpicId, s.Status })
            .HasDatabaseName("ix_sprints_epic_status");

        builder.HasIndex(s => s.StartDate)
            .HasDatabaseName("ix_sprints_start_date")
            .HasFilter("\"StartDate\" IS NOT NULL");
    }
}
