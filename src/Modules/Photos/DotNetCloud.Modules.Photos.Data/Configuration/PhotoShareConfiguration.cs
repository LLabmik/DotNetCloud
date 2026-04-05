using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Photos.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="PhotoShare"/> entity.
/// </summary>
public sealed class PhotoShareConfiguration : IEntityTypeConfiguration<PhotoShare>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PhotoShare> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Permission)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(s => s.Photo)
            .WithMany(p => p.Shares)
            .HasForeignKey(s => s.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Album)
            .WithMany(a => a.Shares)
            .HasForeignKey(s => s.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index by shared user
        builder.HasIndex(s => s.SharedWithUserId)
            .HasDatabaseName("ix_photo_shares_shared_with");

        builder.HasIndex(s => s.PhotoId)
            .HasDatabaseName("ix_photo_shares_photo_id");

        builder.HasIndex(s => s.AlbumId)
            .HasDatabaseName("ix_photo_shares_album_id");
    }
}
