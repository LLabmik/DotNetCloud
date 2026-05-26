using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="VideoSeason"/> entity.
/// </summary>
public sealed class VideoSeasonConfiguration : IEntityTypeConfiguration<VideoSeason>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoSeason> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(300);
        builder.Property(s => s.Overview).HasMaxLength(4000);
        builder.Property(s => s.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(s => s.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.HasMany(s => s.Episodes)
            .WithOne(e => e.Season)
            .HasForeignKey(e => e.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.SeriesId).HasDatabaseName("ix_video_seasons_series_id");
        builder.HasIndex(s => new { s.SeriesId, s.SeasonNumber })
            .IsUnique()
            .HasDatabaseName("uq_video_seasons_series_season_number");
    }
}
