using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Photos.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="PhotoTag"/> entity.
/// </summary>
public sealed class PhotoTagConfiguration : IEntityTypeConfiguration<PhotoTag>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PhotoTag> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Tag)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(t => t.Photo)
            .WithMany(p => p.Tags)
            .HasForeignKey(t => t.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: one tag value per photo
        builder.HasIndex(t => new { t.PhotoId, t.Tag })
            .IsUnique()
            .HasDatabaseName("uq_photo_tags_photo_tag");

        builder.HasIndex(t => t.Tag)
            .HasDatabaseName("ix_photo_tags_tag");
    }
}
