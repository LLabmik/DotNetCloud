using DotNetCloud.Modules.Bookmarks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Bookmarks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="BookmarkItem"/> entity.
/// </summary>
public sealed class BookmarkItemConfiguration : IEntityTypeConfiguration<BookmarkItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BookmarkItem> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Url).IsRequired().HasMaxLength(2048);
        builder.Property(b => b.NormalizedUrl).IsRequired().HasMaxLength(2048);
        builder.Property(b => b.Title).IsRequired().HasMaxLength(500);
        builder.Property(b => b.Description).HasMaxLength(2000);
        builder.Property(b => b.Notes).HasColumnType("text");
        builder.Property(b => b.TagsJson).HasColumnType("jsonb");
        builder.Property(b => b.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(b => b.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasQueryFilter(b => !b.IsDeleted);

        builder.HasOne(b => b.Folder)
            .WithMany(f => f.Bookmarks)
            .HasForeignKey(b => b.FolderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(b => b.Preview)
            .WithOne(p => p.Bookmark)
            .HasForeignKey<BookmarkPreview>(p => p.BookmarkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.OwnerId).HasDatabaseName("ix_bookmarks_owner_id");
        builder.HasIndex(b => new { b.OwnerId, b.FolderId }).HasDatabaseName("ix_bookmarks_owner_folder");
        builder.HasIndex(b => b.NormalizedUrl).HasDatabaseName("ix_bookmarks_normalized_url");
    }
}
