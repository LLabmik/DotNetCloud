using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Photos.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Photo"/> entity.
/// </summary>
public sealed class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Thumbnail blobs — not loaded by default to keep queries fast
        builder.Property(p => p.ThumbnailGrid)
            .HasColumnType("bytea");

        builder.Property(p => p.ThumbnailDetail)
            .HasColumnType("bytea");

        // Soft-delete query filter
        builder.HasQueryFilter(p => !p.IsDeleted);

        // One-to-one: Photo → PhotoMetadata
        builder.HasOne(p => p.Metadata)
            .WithOne(m => m!.Photo!)
            .HasForeignKey<PhotoMetadata>(m => m.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(p => p.OwnerId)
            .HasDatabaseName("ix_photos_owner_id");

        builder.HasIndex(p => p.FileNodeId)
            .IsUnique()
            .HasDatabaseName("uq_photos_file_node_id");

        builder.HasIndex(p => new { p.OwnerId, p.TakenAt })
            .HasDatabaseName("ix_photos_owner_taken_at");

        builder.HasIndex(p => new { p.OwnerId, p.IsFavorite })
            .HasDatabaseName("ix_photos_owner_favorite");

        builder.HasIndex(p => p.IsDeleted)
            .HasDatabaseName("ix_photos_is_deleted");

        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("ix_photos_created_at");
    }
}
