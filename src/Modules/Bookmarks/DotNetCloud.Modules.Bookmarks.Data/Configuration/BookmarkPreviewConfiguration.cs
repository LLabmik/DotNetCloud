using DotNetCloud.Modules.Bookmarks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Bookmarks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="BookmarkPreview"/> entity.
/// </summary>
public sealed class BookmarkPreviewConfiguration : IEntityTypeConfiguration<BookmarkPreview>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BookmarkPreview> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.CanonicalUrl).HasMaxLength(2048);
        builder.Property(p => p.SiteName).HasMaxLength(200);
        builder.Property(p => p.ResolvedTitle).HasMaxLength(500);
        builder.Property(p => p.ResolvedDescription).HasMaxLength(2000);
        builder.Property(p => p.FaviconUrl).HasMaxLength(2048);
        builder.Property(p => p.PreviewImageUrl).HasMaxLength(2048);
        builder.Property(p => p.ContentType).HasMaxLength(100);
        builder.Property(p => p.ErrorMessage).HasMaxLength(1000);
        builder.Property(p => p.ETag).HasMaxLength(200);
        builder.Property(p => p.LastModified).HasMaxLength(100);
        builder.Property(p => p.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(p => p.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(p => p.BookmarkId).IsUnique().HasDatabaseName("ix_bookmark_previews_bookmark_id");
        builder.HasIndex(p => p.Status).HasDatabaseName("ix_bookmark_previews_status");
    }
}
