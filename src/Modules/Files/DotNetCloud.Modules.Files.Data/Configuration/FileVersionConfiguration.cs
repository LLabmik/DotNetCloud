using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="FileVersion"/> entity.
/// </summary>
public sealed class FileVersionConfiguration : IEntityTypeConfiguration<FileVersion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileVersion> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.ContentHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(v => v.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(v => v.MimeType)
            .HasMaxLength(255);

        builder.Property(v => v.Label)
            .HasMaxLength(200);

        builder.Property(v => v.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(v => v.FileNode)
            .WithMany(n => n.Versions)
            .HasForeignKey(v => v.FileNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(v => v.FileNodeId)
            .HasDatabaseName("ix_file_versions_file_node_id");

        builder.HasIndex(v => new { v.FileNodeId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ix_file_versions_node_version");

        builder.HasIndex(v => v.ContentHash)
            .HasDatabaseName("ix_file_versions_content_hash");

        builder.HasIndex(v => v.CreatedByUserId)
            .HasDatabaseName("ix_file_versions_created_by");
    }
}
