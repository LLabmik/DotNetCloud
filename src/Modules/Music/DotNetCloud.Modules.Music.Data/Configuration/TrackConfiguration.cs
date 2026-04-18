using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Track"/> entity.
/// </summary>
public sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
        builder.Property(t => t.MimeType).IsRequired().HasMaxLength(100);
        builder.Property(t => t.FileName).IsRequired().HasMaxLength(255);
        builder.Property(t => t.MusicBrainzRecordingId).HasMaxLength(36);
        builder.Property(t => t.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(t => t.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasOne(t => t.Album)
            .WithMany(a => a.Tracks)
            .HasForeignKey(t => t.AlbumId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.FileNodeId).IsUnique().HasDatabaseName("uq_tracks_file_node_id");
        builder.HasIndex(t => t.OwnerId).HasDatabaseName("ix_tracks_owner_id");
        builder.HasIndex(t => t.Title).HasDatabaseName("ix_tracks_title");
        builder.HasIndex(t => t.AlbumId).HasDatabaseName("ix_tracks_album_id");
        builder.HasIndex(t => new { t.OwnerId, t.CreatedAt }).HasDatabaseName("ix_tracks_owner_created_at");
        builder.HasIndex(t => t.IsDeleted).HasDatabaseName("ix_tracks_is_deleted");
        builder.HasIndex(t => t.MusicBrainzRecordingId).HasDatabaseName("ix_tracks_musicbrainz_recording_id");
    }
}
