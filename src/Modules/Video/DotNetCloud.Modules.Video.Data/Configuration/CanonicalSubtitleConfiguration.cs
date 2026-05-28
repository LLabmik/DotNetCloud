using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalSubtitle"/> entity.
/// </summary>
public sealed class CanonicalSubtitleConfiguration : IEntityTypeConfiguration<CanonicalSubtitle>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalSubtitle> builder)
    {
        builder.ToTable("canonical_subtitles");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.VideoContentHash).IsRequired().HasMaxLength(64);
        builder.Property(s => s.Language).IsRequired().HasMaxLength(20);
        builder.Property(s => s.Label).HasMaxLength(100);
        builder.Property(s => s.Format).IsRequired().HasMaxLength(10);

        builder.HasOne(s => s.Video)
            .WithMany(v => v.Subtitles)
            .HasForeignKey(s => s.VideoContentHash)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.VideoContentHash).HasDatabaseName("ix_canonical_subtitles_video_content_hash");
    }
}
