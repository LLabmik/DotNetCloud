using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="FileTag"/> entity.
/// </summary>
public sealed class FileTagConfiguration : IEntityTypeConfiguration<FileTag>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileTag> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Color)
            .HasMaxLength(7);

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(t => t.FileNode)
            .WithMany(n => n.Tags)
            .HasForeignKey(t => t.FileNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: same tag name on the same node by the same user
        builder.HasIndex(t => new { t.FileNodeId, t.Name, t.CreatedByUserId })
            .IsUnique()
            .HasDatabaseName("ix_file_tags_node_name_user");

        builder.HasIndex(t => t.Name)
            .HasDatabaseName("ix_file_tags_name");

        builder.HasIndex(t => t.CreatedByUserId)
            .HasDatabaseName("ix_file_tags_created_by");
    }
}
