using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="VideoSeriesItem"/> junction entity.
/// </summary>
public sealed class VideoSeriesItemConfiguration : IEntityTypeConfiguration<VideoSeriesItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoSeriesItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.EpisodeTitle).HasMaxLength(500);
        builder.Property(i => i.AddedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(i => i.Series)
            .WithMany(s => s.Items)
            .HasForeignKey(i => i.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Video)
            .WithMany()
            .HasForeignKey(i => i.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.SeriesId, i.VideoId })
            .IsUnique()
            .HasDatabaseName("uq_video_series_items_series_video");

        builder.HasIndex(i => i.SeriesId).HasDatabaseName("ix_video_series_items_series_id");
        builder.HasIndex(i => i.VideoId).HasDatabaseName("ix_video_series_items_video_id");
    }
}
