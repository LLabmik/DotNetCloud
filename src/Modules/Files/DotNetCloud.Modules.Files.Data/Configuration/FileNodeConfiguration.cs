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

        builder.Property(n => n.PosixOwnerHint)
            .HasMaxLength(200);
    }
}
