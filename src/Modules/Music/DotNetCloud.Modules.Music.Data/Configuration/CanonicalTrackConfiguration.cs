using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalTrack"/> entity.
/// </summary>
public sealed class CanonicalTrackConfiguration : IEntityTypeConfiguration<CanonicalTrack>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalTrack> builder)
    {
        builder.ToTable("canonical_tracks");
        builder.HasKey(t => t.ContentHash);

        builder.Property(t => t.ContentHash).IsRequired().HasMaxLength(64);
        builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
        builder.Property(t => t.MimeType).IsRequired().HasMaxLength(100);
        builder.Property(t => t.MusicBrainzRecordingId).HasMaxLength(36);
        builder.Property(t => t.Isrc).HasMaxLength(20);
        builder.Property(t => t.Composers).HasMaxLength(2000);
        builder.Property(t => t.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(t => t.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(t => t.Title).HasDatabaseName("ix_canonical_tracks_title");
        builder.HasIndex(t => t.MusicBrainzRecordingId).HasDatabaseName("ix_canonical_tracks_musicbrainz_recording_id");
        builder.HasIndex(t => t.Isrc).HasDatabaseName("ix_canonical_tracks_isrc");
    }
}
