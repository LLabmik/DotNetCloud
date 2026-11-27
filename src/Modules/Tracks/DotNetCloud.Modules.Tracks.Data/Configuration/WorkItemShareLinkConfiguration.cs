using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class WorkItemShareLinkConfiguration : IEntityTypeConfiguration<WorkItemShareLink>
{
    public void Configure(EntityTypeBuilder<WorkItemShareLink> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Token)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(l => l.Permission)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(l => l.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(l => l.WorkItem)
            .WithMany()
            .HasForeignKey(l => l.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.Token)
            .IsUnique()
            .HasDatabaseName("ix_work_item_share_links_token");

        builder.HasIndex(l => l.WorkItemId)
            .HasDatabaseName("ix_work_item_share_links_work_item");

        builder.HasIndex(l => l.IsActive)
            .HasDatabaseName("ix_work_item_share_links_active");
    }
}
