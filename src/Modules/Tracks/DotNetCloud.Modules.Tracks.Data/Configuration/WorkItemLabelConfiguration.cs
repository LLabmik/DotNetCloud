using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class WorkItemLabelConfiguration : IEntityTypeConfiguration<WorkItemLabel>
{
    public void Configure(EntityTypeBuilder<WorkItemLabel> builder)
    {
        builder.HasKey(wl => new { wl.WorkItemId, wl.LabelId });

        builder.Property(wl => wl.AppliedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(wl => wl.WorkItem)
            .WithMany(wi => wi.WorkItemLabels)
            .HasForeignKey(wl => wl.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wl => wl.Label)
            .WithMany(l => l.WorkItemLabels)
            .HasForeignKey(wl => wl.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
