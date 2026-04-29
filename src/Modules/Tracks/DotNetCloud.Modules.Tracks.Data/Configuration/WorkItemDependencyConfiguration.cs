using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class WorkItemDependencyConfiguration : IEntityTypeConfiguration<WorkItemDependency>
{
    public void Configure(EntityTypeBuilder<WorkItemDependency> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(d => d.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(d => d.WorkItem)
            .WithMany(wi => wi.Dependencies)
            .HasForeignKey(d => d.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.DependsOnWorkItem)
            .WithMany(wi => wi.Dependents)
            .HasForeignKey(d => d.DependsOnWorkItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.WorkItemId, d.DependsOnWorkItemId, d.Type })
            .IsUnique()
            .HasDatabaseName("uq_work_item_dependencies_item_depends_type");

        builder.HasIndex(d => d.DependsOnWorkItemId)
            .HasDatabaseName("ix_work_item_dependencies_depends_on");
    }
}
