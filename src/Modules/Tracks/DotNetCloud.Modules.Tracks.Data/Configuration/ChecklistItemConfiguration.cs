using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ChecklistItem"/> entity.
/// </summary>
public sealed class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.Position)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(i => i.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(i => i.Checklist)
            .WithMany(cl => cl.Items)
            .HasForeignKey(i => i.ChecklistId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(i => new { i.ChecklistId, i.Position })
            .HasDatabaseName("ix_checklist_items_checklist_position");

        builder.HasIndex(i => i.AssignedToUserId)
            .HasDatabaseName("ix_checklist_items_assigned_to")
            .HasFilter("\"AssignedToUserId\" IS NOT NULL");
    }
}
