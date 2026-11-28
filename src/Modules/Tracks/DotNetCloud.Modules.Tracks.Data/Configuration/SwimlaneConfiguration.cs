using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class SwimlaneConfiguration : IEntityTypeConfiguration<Swimlane>
{
    public void Configure(EntityTypeBuilder<Swimlane> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ContainerType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Position)
            .IsRequired();

        builder.Property(s => s.Color)
            .HasMaxLength(20);

        builder.Property(s => s.IsDone)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(s => new { s.ContainerType, s.ContainerId, s.Position })
            .HasDatabaseName("ix_swimlanes_container_position");

        builder.HasIndex(s => s.IsArchived)
            .HasDatabaseName("ix_swimlanes_is_archived");
    }
}
