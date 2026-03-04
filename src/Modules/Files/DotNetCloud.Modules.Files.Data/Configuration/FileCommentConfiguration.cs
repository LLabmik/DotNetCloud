using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="FileComment"/> entity.
/// </summary>
public sealed class FileCommentConfiguration : IEntityTypeConfiguration<FileComment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileComment> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(c => c.FileNode)
            .WithMany(n => n.Comments)
            .HasForeignKey(c => c.FileNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referencing for threaded replies
        builder.HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft-delete query filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Indexes
        builder.HasIndex(c => c.FileNodeId)
            .HasDatabaseName("ix_file_comments_file_node_id");

        builder.HasIndex(c => c.ParentCommentId)
            .HasDatabaseName("ix_file_comments_parent_id");

        builder.HasIndex(c => c.CreatedByUserId)
            .HasDatabaseName("ix_file_comments_created_by");

        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("ix_file_comments_created_at");
    }
}
