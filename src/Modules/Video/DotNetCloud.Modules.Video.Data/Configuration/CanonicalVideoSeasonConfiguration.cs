using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalVideoSeason"/> entity.
/// </summary>
public sealed class CanonicalVideoSeasonConfiguration : IEntityTypeConfiguration<CanonicalVideoSeason>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalVideoSeason> builder)
    {
        builder.ToTable("canonical_video_seasons");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(300);
        builder.Property(s => s.Overview).HasMaxLength(4000);
        builder.Property(s => s.PosterHash).HasMaxLength(64);
        builder.Property(s => s.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(s => s.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(s => s.Series)
            .WithMany(s => s.Seasons)
            .HasForeignKey(s => s.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.SeriesId).HasDatabaseName("ix_canonical_video_seasons_series_id");
        builder.HasIndex(s => new { s.SeriesId, s.SeasonNumber }).IsUnique().HasDatabaseName("uq_canonical_video_seasons_series_season");
    }
}
