using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="TrackArtist"/> junction entity.
/// </summary>
public sealed class TrackArtistConfiguration : IEntityTypeConfiguration<TrackArtist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TrackArtist> builder)
    {
        builder.HasKey(ta => new { ta.TrackId, ta.ArtistId });

        builder.HasOne(ta => ta.Track)
            .WithMany(t => t.TrackArtists)
            .HasForeignKey(ta => ta.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ta => ta.Artist)
            .WithMany(a => a.TrackArtists)
            .HasForeignKey(ta => ta.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ta => ta.ArtistId).HasDatabaseName("ix_track_artists_artist_id");
        builder.HasIndex(ta => new { ta.TrackId, ta.IsPrimary }).HasDatabaseName("ix_track_artists_track_primary");
    }
}
