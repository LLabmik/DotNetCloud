using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Photos.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="PhotoMetadata"/> entity.
/// </summary>
public sealed class PhotoMetadataConfiguration : IEntityTypeConfiguration<PhotoMetadata>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PhotoMetadata> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.CameraMake).HasMaxLength(100);
        builder.Property(m => m.CameraModel).HasMaxLength(100);
        builder.Property(m => m.LensModel).HasMaxLength(200);
        builder.Property(m => m.ShutterSpeed).HasMaxLength(50);

        // Index for geo queries
        builder.HasIndex(m => new { m.Latitude, m.Longitude })
            .HasDatabaseName("ix_photo_metadata_geo")
            .HasFilter("\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL");
    }
}
