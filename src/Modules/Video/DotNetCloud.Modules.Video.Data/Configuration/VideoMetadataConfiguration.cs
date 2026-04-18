using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="VideoMetadata"/> entity.
/// </summary>
public sealed class VideoMetadataConfiguration : IEntityTypeConfiguration<VideoMetadata>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoMetadata> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.VideoCodec).HasMaxLength(50);
        builder.Property(m => m.AudioCodec).HasMaxLength(50);
        builder.Property(m => m.ContainerFormat).HasMaxLength(20);
        builder.Property(m => m.ExtractedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(m => m.Video)
            .WithOne(v => v.Metadata)
            .HasForeignKey<VideoMetadata>(m => m.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.VideoId).IsUnique().HasDatabaseName("uq_video_metadata_video_id");
    }
}
