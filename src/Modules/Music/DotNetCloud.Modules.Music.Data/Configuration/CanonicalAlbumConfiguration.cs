using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalAlbum"/> entity.
/// </summary>
public sealed class CanonicalAlbumConfiguration : IEntityTypeConfiguration<CanonicalAlbum>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalAlbum> builder)
    {
        builder.ToTable("canonical_albums");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired().HasMaxLength(500);
        builder.Property(a => a.CoverArtHash).HasMaxLength(64);
        builder.Property(a => a.MusicBrainzReleaseGroupId).HasMaxLength(36);
        builder.Property(a => a.MusicBrainzReleaseId).HasMaxLength(36);
        builder.Property(a => a.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(a => a.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(a => a.Title).HasDatabaseName("ix_canonical_albums_title");
        builder.HasIndex(a => a.Year).HasDatabaseName("ix_canonical_albums_year");
        builder.HasIndex(a => a.MusicBrainzReleaseGroupId).HasDatabaseName("ix_canonical_albums_musicbrainz_release_group_id");
    }
}
