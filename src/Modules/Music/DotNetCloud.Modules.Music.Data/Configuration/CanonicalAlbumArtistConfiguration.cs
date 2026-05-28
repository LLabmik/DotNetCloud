using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalAlbumArtist"/> junction entity.
/// </summary>
public sealed class CanonicalAlbumArtistConfiguration : IEntityTypeConfiguration<CanonicalAlbumArtist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalAlbumArtist> builder)
    {
        builder.ToTable("canonical_album_artists");
        builder.HasKey(aa => new { aa.AlbumId, aa.ArtistId });

        builder.HasOne(aa => aa.Album)
            .WithMany(a => a.AlbumArtists)
            .HasForeignKey(aa => aa.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(aa => aa.Artist)
            .WithMany(a => a.AlbumArtists)
            .HasForeignKey(aa => aa.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(aa => aa.ArtistId).HasDatabaseName("ix_canonical_album_artists_artist_id");
        builder.HasIndex(aa => new { aa.AlbumId, aa.IsPrimary }).HasDatabaseName("ix_canonical_album_artists_album_primary");
    }
}
