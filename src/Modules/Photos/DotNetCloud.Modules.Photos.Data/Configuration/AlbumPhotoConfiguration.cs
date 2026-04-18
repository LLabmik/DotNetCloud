using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Photos.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="AlbumPhoto"/> junction entity.
/// </summary>
public sealed class AlbumPhotoConfiguration : IEntityTypeConfiguration<AlbumPhoto>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AlbumPhoto> builder)
    {
        builder.HasKey(ap => new { ap.AlbumId, ap.PhotoId });

        builder.Property(ap => ap.AddedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(ap => ap.Album)
            .WithMany(a => a.AlbumPhotos)
            .HasForeignKey(ap => ap.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ap => ap.Photo)
            .WithMany(p => p.AlbumPhotos)
            .HasForeignKey(ap => ap.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for ordering photos within an album
        builder.HasIndex(ap => new { ap.AlbumId, ap.SortOrder })
            .HasDatabaseName("ix_album_photos_album_sort");
    }
}
