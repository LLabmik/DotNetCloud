using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.HasKey(wi => wi.Id);

        builder.Property(wi => wi.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(wi => wi.ItemNumber)
            .IsRequired();

        builder.Property(wi => wi.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(wi => wi.Description)
            .HasColumnType("text");

        builder.Property(wi => wi.Position)
            .IsRequired();

        builder.Property(wi => wi.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(wi => wi.ETag)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(wi => wi.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(wi => wi.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(wi => wi.Product)
            .WithMany(p => p.WorkItems)
            .HasForeignKey(wi => wi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wi => wi.ParentWorkItem)
            .WithMany(wi => wi.ChildWorkItems)
            .HasForeignKey(wi => wi.ParentWorkItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(wi => wi.Swimlane)
            .WithMany(s => s.WorkItems)
            .HasForeignKey(wi => wi.SwimlaneId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(wi => !wi.IsDeleted);

        // Indexes
        builder.HasIndex(wi => new { wi.ProductId, wi.ItemNumber })
            .IsUnique()
            .HasDatabaseName("uq_work_items_product_number");

        builder.HasIndex(wi => new { wi.SwimlaneId, wi.Position })
            .HasDatabaseName("ix_work_items_swimlane_position");

        builder.HasIndex(wi => new { wi.ProductId, wi.Type })
            .HasDatabaseName("ix_work_items_product_type");

        builder.HasIndex(wi => wi.ParentWorkItemId)
            .HasDatabaseName("ix_work_items_parent");

        builder.HasIndex(wi => wi.CreatedByUserId)
            .HasDatabaseName("ix_work_items_created_by");

        builder.HasIndex(wi => wi.DueDate)
            .HasDatabaseName("ix_work_items_due_date")
            .HasFilter("\"DueDate\" IS NOT NULL");

        builder.HasIndex(wi => wi.StartDate)
            .HasDatabaseName("ix_work_items_start_date")
            .HasFilter("\"StartDate\" IS NOT NULL");

        builder.HasIndex(wi => wi.Priority)
            .HasDatabaseName("ix_work_items_priority");

        builder.HasIndex(wi => wi.IsArchived)
            .HasDatabaseName("ix_work_items_is_archived");

        builder.HasIndex(wi => wi.IsDeleted)
            .HasDatabaseName("ix_work_items_is_deleted");

        builder.HasIndex(wi => wi.CreatedAt)
            .HasDatabaseName("ix_work_items_created_at");
    }
}
