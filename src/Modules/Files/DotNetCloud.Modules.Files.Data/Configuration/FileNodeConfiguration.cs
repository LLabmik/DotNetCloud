using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="FileNode"/> entity.
/// </summary>
public sealed class FileNodeConfiguration : IEntityTypeConfiguration<FileNode>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileNode> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(n => n.NodeType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(n => n.MimeType)
            .HasMaxLength(255);

        builder.Property(n => n.MaterializedPath)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(n => n.ContentHash)
            .HasMaxLength(64);

        builder.Property(n => n.StoragePath)
            .HasMaxLength(1000);

        builder.Property(n => n.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(n => n.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Self-referencing parent-child relationship
        builder.HasOne(n => n.Parent)
            .WithMany(n => n.Children)
            .HasForeignKey(n => n.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft-delete query filter
        builder.HasQueryFilter(n => !n.IsDeleted);

        // Indexes
        builder.HasIndex(n => n.OwnerId)
            .HasDatabaseName("ix_file_nodes_owner_id");

        builder.HasIndex(n => n.ParentId)
            .HasDatabaseName("ix_file_nodes_parent_id");

        builder.HasIndex(n => n.MaterializedPath)
            .HasDatabaseName("ix_file_nodes_materialized_path");

        builder.HasIndex(n => new { n.ParentId, n.Name })
            .HasDatabaseName("ix_file_nodes_parent_name");

        // Unique constraint: only one active (non-deleted) file per name per parent folder.
        // This prevents race conditions where concurrent uploads create duplicate entries.
        // PostgreSQL treats NULLs as distinct in unique indexes, so root-level nodes
        // (ParentId IS NULL) are covered — each user+name combo is still unique because
        // root queries also filter by OwnerId.
        builder.HasIndex(n => new { n.ParentId, n.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"ParentId\" IS NOT NULL")
            .HasDatabaseName("uq_file_nodes_parent_name_active");

        // Separate unique index for root-level nodes (ParentId IS NULL) scoped by owner.
        // Without this, two users could not both have a root-level "Documents" folder,
        // and two concurrent uploads to root for the same user would not be caught.
        builder.HasIndex(n => new { n.OwnerId, n.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"ParentId\" IS NULL")
            .HasDatabaseName("uq_file_nodes_root_name_active");

        builder.HasIndex(n => n.ContentHash)
            .HasDatabaseName("ix_file_nodes_content_hash");

        builder.HasIndex(n => n.IsDeleted)
            .HasDatabaseName("ix_file_nodes_is_deleted");

        builder.HasIndex(n => new { n.OwnerId, n.IsFavorite })
            .HasDatabaseName("ix_file_nodes_owner_favorite");

        builder.HasIndex(n => n.CreatedAt)
            .HasDatabaseName("ix_file_nodes_created_at");

        builder.HasIndex(n => n.UpdatedAt)
            .HasDatabaseName("ix_file_nodes_updated_at");

        builder.HasIndex(n => new { n.OwnerId, n.SyncSequence })
            .HasDatabaseName("ix_file_nodes_owner_sync_sequence");

        // Optional FK to the device that created/last modified this node
        builder.HasOne(n => n.OriginatingDevice)
            .WithMany()
            .HasForeignKey(n => n.OriginatingDeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(n => n.OriginatingDeviceId)
            .HasDatabaseName("ix_file_nodes_originating_device_id")
            .HasFilter("\"OriginatingDeviceId\" IS NOT NULL");

        builder.Property(n => n.PosixOwnerHint)
            .HasMaxLength(200);
    }
}
