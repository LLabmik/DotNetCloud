using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class WorkItemAssignmentConfiguration : IEntityTypeConfiguration<WorkItemAssignment>
{
    public void Configure(EntityTypeBuilder<WorkItemAssignment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AssignedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(a => a.WorkItem)
            .WithMany(wi => wi.Assignments)
            .HasForeignKey(a => a.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.WorkItemId, a.UserId })
            .IsUnique()
            .HasDatabaseName("uq_work_item_assignments_item_user");

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("ix_work_item_assignments_user_id");
    }
}
