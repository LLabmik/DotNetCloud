using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="MusicAlbum"/> entity.
/// </summary>
public sealed class MusicAlbumConfiguration : IEntityTypeConfiguration<MusicAlbum>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MusicAlbum> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired().HasMaxLength(500);
        builder.Property(a => a.CoverArtPath).HasMaxLength(1000);
        builder.Property(a => a.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(a => a.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasOne(a => a.Artist)
            .WithMany(ar => ar.Albums)
            .HasForeignKey(a => a.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ArtistId).HasDatabaseName("ix_music_albums_artist_id");
        builder.HasIndex(a => a.OwnerId).HasDatabaseName("ix_music_albums_owner_id");
        builder.HasIndex(a => a.Title).HasDatabaseName("ix_music_albums_title");
        builder.HasIndex(a => a.Year).HasDatabaseName("ix_music_albums_year");
        builder.HasIndex(a => a.IsDeleted).HasDatabaseName("ix_music_albums_is_deleted");
    }
}
