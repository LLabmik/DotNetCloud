using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalVideoEpisode"/> entity.
/// </summary>
public sealed class CanonicalVideoEpisodeConfiguration : IEntityTypeConfiguration<CanonicalVideoEpisode>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalVideoEpisode> builder)
    {
        builder.ToTable("canonical_video_episodes");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.VideoContentHash).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Title).HasMaxLength(500);
        builder.Property(e => e.Overview).HasMaxLength(4000);

        builder.HasOne(e => e.Season)
            .WithMany(s => s.Episodes)
            .HasForeignKey(e => e.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.SeasonId).HasDatabaseName("ix_canonical_video_episodes_season_id");
        builder.HasIndex(e => new { e.SeasonId, e.EpisodeNumber }).IsUnique().HasDatabaseName("uq_canonical_video_episodes_season_episode");
        builder.HasIndex(e => e.VideoContentHash).HasDatabaseName("ix_canonical_video_episodes_video_content_hash");
    }
}
