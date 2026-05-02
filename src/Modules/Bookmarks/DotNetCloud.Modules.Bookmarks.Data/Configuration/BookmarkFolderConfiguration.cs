using DotNetCloud.Modules.Bookmarks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Bookmarks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="BookmarkFolder"/> entity.
/// </summary>
public sealed class BookmarkFolderConfiguration : IEntityTypeConfiguration<BookmarkFolder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BookmarkFolder> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Name).IsRequired().HasMaxLength(300);
        builder.Property(f => f.Color).HasMaxLength(20);
        builder.Property(f => f.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(f => f.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasQueryFilter(f => !f.IsDeleted);

        builder.HasOne(f => f.Parent)
            .WithMany(f => f.Children)
            .HasForeignKey(f => f.ParentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(f => f.OwnerId).HasDatabaseName("ix_bookmark_folders_owner_id");
        builder.HasIndex(f => new { f.OwnerId, f.ParentId }).HasDatabaseName("ix_bookmark_folders_owner_parent");
    }
}
