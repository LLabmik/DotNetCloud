using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalVideoMetadata"/> entity.
/// </summary>
public sealed class CanonicalVideoMetadataConfiguration : IEntityTypeConfiguration<CanonicalVideoMetadata>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalVideoMetadata> builder)
    {
        builder.ToTable("canonical_video_metadata");
        builder.HasKey(m => m.VideoContentHash);

        builder.Property(m => m.VideoContentHash).IsRequired().HasMaxLength(64);
        builder.Property(m => m.VideoCodec).HasMaxLength(50);
        builder.Property(m => m.AudioCodec).HasMaxLength(50);
        builder.Property(m => m.ContainerFormat).HasMaxLength(20);
        builder.Property(m => m.ExtractedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(m => m.Video)
            .WithOne(v => v.Metadata)
            .HasForeignKey<CanonicalVideoMetadata>(m => m.VideoContentHash)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
