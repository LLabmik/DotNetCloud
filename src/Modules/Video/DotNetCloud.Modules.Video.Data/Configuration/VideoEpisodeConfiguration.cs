using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="VideoEpisode"/> junction entity.
/// </summary>
public sealed class VideoEpisodeConfiguration : IEntityTypeConfiguration<VideoEpisode>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoEpisode> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).HasMaxLength(500);
        builder.Property(e => e.Overview).HasMaxLength(4000);
        builder.Property(e => e.AddedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(e => e.Season)
            .WithMany(s => s.Episodes)
            .HasForeignKey(e => e.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Video)
            .WithMany()
            .HasForeignKey(e => e.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SeasonId, e.VideoId })
            .IsUnique()
            .HasDatabaseName("uq_video_episodes_season_video");

        builder.HasIndex(e => e.SeasonId).HasDatabaseName("ix_video_episodes_season_id");
        builder.HasIndex(e => e.VideoId).HasDatabaseName("ix_video_episodes_video_id");
    }
}
