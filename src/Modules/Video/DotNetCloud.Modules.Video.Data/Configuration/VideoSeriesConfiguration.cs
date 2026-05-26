using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="VideoSeries"/> entity.
/// </summary>
public sealed class VideoSeriesConfiguration : IEntityTypeConfiguration<VideoSeries>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoSeries> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(300);
        builder.Property(s => s.Description).HasMaxLength(4000);
        builder.Property(s => s.ExternalPosterPath).HasMaxLength(500);
        builder.Property(s => s.TmdbName).HasMaxLength(300);
        builder.Property(s => s.Genres).HasMaxLength(500);
        builder.Property(s => s.Status).HasMaxLength(100);
        builder.Property(s => s.Type).IsRequired().HasConversion<int>();
        builder.Property(s => s.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(s => s.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.HasMany(s => s.Seasons)
            .WithOne(s => s.Series)
            .HasForeignKey(s => s.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Items)
            .WithOne(i => i.Series)
            .HasForeignKey(i => i.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.OwnerId).HasDatabaseName("ix_video_series_owner_id");
        builder.HasIndex(s => s.Name).HasDatabaseName("ix_video_series_name");
        builder.HasIndex(s => s.TmdbId).HasDatabaseName("ix_video_series_tmdb_id");
    }
}
