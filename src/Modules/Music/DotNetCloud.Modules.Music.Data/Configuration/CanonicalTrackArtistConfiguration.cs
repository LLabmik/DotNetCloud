using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalTrackArtist"/> junction entity.
/// </summary>
public sealed class CanonicalTrackArtistConfiguration : IEntityTypeConfiguration<CanonicalTrackArtist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalTrackArtist> builder)
    {
        builder.ToTable("canonical_track_artists");
        builder.HasKey(ta => new { ta.TrackContentHash, ta.ArtistId });

        builder.Property(ta => ta.TrackContentHash).IsRequired().HasMaxLength(64);

        builder.HasOne(ta => ta.Track)
            .WithMany(t => t.TrackArtists)
            .HasForeignKey(ta => ta.TrackContentHash)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ta => ta.Artist)
            .WithMany(a => a.TrackArtists)
            .HasForeignKey(ta => ta.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ta => ta.ArtistId).HasDatabaseName("ix_canonical_track_artists_artist_id");
        builder.HasIndex(ta => new { ta.TrackContentHash, ta.IsPrimary }).HasDatabaseName("ix_canonical_track_artists_track_primary");
    }
}
