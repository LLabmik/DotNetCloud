using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="TrackGenre"/> junction entity.
/// </summary>
public sealed class TrackGenreConfiguration : IEntityTypeConfiguration<TrackGenre>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TrackGenre> builder)
    {
        builder.HasKey(tg => new { tg.TrackId, tg.GenreId });

        builder.HasOne(tg => tg.Track)
            .WithMany(t => t.TrackGenres)
            .HasForeignKey(tg => tg.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tg => tg.Genre)
            .WithMany(g => g.TrackGenres)
            .HasForeignKey(tg => tg.GenreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tg => tg.GenreId).HasDatabaseName("ix_track_genres_genre_id");
    }
}
