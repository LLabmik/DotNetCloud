using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalArtist"/> entity.
/// </summary>
public sealed class CanonicalArtistConfiguration : IEntityTypeConfiguration<CanonicalArtist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalArtist> builder)
    {
        builder.ToTable("canonical_artists");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(500);
        builder.Property(a => a.SortName).HasMaxLength(500);
        builder.Property(a => a.MusicBrainzId).HasMaxLength(36);
        builder.Property(a => a.Biography).HasMaxLength(10000);
        builder.Property(a => a.ImageUrl).HasMaxLength(2000);
        builder.Property(a => a.WikipediaUrl).HasMaxLength(2000);
        builder.Property(a => a.DiscogsUrl).HasMaxLength(2000);
        builder.Property(a => a.OfficialUrl).HasMaxLength(2000);
        builder.Property(a => a.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(a => a.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(a => a.Name).HasDatabaseName("ix_canonical_artists_name");
        builder.HasIndex(a => a.MusicBrainzId).IsUnique().HasDatabaseName("uq_canonical_artists_musicbrainz_id");
        builder.HasIndex(a => new { a.Name, a.MusicBrainzId }).HasDatabaseName("ix_canonical_artists_name_mbid");
    }
}
