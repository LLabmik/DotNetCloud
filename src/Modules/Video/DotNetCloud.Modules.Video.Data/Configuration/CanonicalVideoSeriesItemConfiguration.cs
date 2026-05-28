using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalVideoSeriesItem"/> entity.
/// </summary>
public sealed class CanonicalVideoSeriesItemConfiguration : IEntityTypeConfiguration<CanonicalVideoSeriesItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalVideoSeriesItem> builder)
    {
        builder.ToTable("canonical_video_series_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.VideoContentHash).IsRequired().HasMaxLength(64);
        builder.Property(i => i.EpisodeTitle).HasMaxLength(500);

        builder.HasOne(i => i.Series)
            .WithMany(s => s.Items)
            .HasForeignKey(i => i.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.SeriesId).HasDatabaseName("ix_canonical_video_series_items_series_id");
        builder.HasIndex(i => i.VideoContentHash).HasDatabaseName("ix_canonical_video_series_items_video_content_hash");
    }
}
