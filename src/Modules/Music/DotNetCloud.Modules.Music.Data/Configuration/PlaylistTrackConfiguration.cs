using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="PlaylistTrack"/> junction entity.
/// </summary>
public sealed class PlaylistTrackConfiguration : IEntityTypeConfiguration<PlaylistTrack>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlaylistTrack> builder)
    {
        builder.HasKey(pt => new { pt.PlaylistId, pt.TrackId });

        builder.Property(pt => pt.AddedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(pt => pt.Playlist)
            .WithMany(p => p.PlaylistTracks)
            .HasForeignKey(pt => pt.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pt => pt.Track)
            .WithMany(t => t.PlaylistTracks)
            .HasForeignKey(pt => pt.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pt => new { pt.PlaylistId, pt.SortOrder })
            .HasDatabaseName("ix_playlist_tracks_playlist_sort");
    }
}
