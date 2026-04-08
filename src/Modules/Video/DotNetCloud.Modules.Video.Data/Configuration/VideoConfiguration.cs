using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Models.Video"/> entity.
/// </summary>
public sealed class VideoConfiguration : IEntityTypeConfiguration<Models.Video>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Models.Video> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Title).IsRequired().HasMaxLength(500);
        builder.Property(v => v.FileName).IsRequired().HasMaxLength(255);
        builder.Property(v => v.MimeType).IsRequired().HasMaxLength(100);
        builder.Property(v => v.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(v => v.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(v => !v.IsDeleted);

        // Thumbnail blob — no max length (variable-size JPEG)
        builder.Property(v => v.ThumbnailPoster).HasColumnName("thumbnail_poster");

        builder.HasIndex(v => v.FileNodeId).IsUnique().HasDatabaseName("uq_videos_file_node_id");
        builder.HasIndex(v => v.OwnerId).HasDatabaseName("ix_videos_owner_id");
        builder.HasIndex(v => v.Title).HasDatabaseName("ix_videos_title");
        builder.HasIndex(v => new { v.OwnerId, v.CreatedAt }).HasDatabaseName("ix_videos_owner_created_at");
        builder.HasIndex(v => v.IsDeleted).HasDatabaseName("ix_videos_is_deleted");
    }
}
