using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class SprintItemConfiguration : IEntityTypeConfiguration<SprintItem>
{
    public void Configure(EntityTypeBuilder<SprintItem> builder)
    {
        builder.HasKey(si => new { si.SprintId, si.ItemId });

        builder.Property(si => si.AddedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(si => si.Sprint)
            .WithMany(s => s.SprintItems)
            .HasForeignKey(si => si.SprintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(si => si.Item)
            .WithMany(wi => wi.SprintItems)
            .HasForeignKey(si => si.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(si => si.ItemId)
            .HasDatabaseName("ix_sprint_items_item_id");
    }
}
