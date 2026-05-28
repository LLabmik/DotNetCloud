using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalTrackGenre"/> junction entity.
/// </summary>
public sealed class CanonicalTrackGenreConfiguration : IEntityTypeConfiguration<CanonicalTrackGenre>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalTrackGenre> builder)
    {
        builder.ToTable("canonical_track_genres");
        builder.HasKey(tg => new { tg.TrackContentHash, tg.GenreId });

        builder.Property(tg => tg.TrackContentHash).IsRequired().HasMaxLength(64);

        builder.HasOne(tg => tg.Track)
            .WithMany(t => t.TrackGenres)
            .HasForeignKey(tg => tg.TrackContentHash)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tg => tg.Genre)
            .WithMany(g => g.TrackGenres)
            .HasForeignKey(tg => tg.GenreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tg => tg.GenreId).HasDatabaseName("ix_canonical_track_genres_genre_id");
    }
}
