using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalVideo"/> entity.
/// </summary>
public sealed class CanonicalVideoConfiguration : IEntityTypeConfiguration<CanonicalVideo>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalVideo> builder)
    {
        builder.ToTable("canonical_videos");
        builder.HasKey(v => v.ContentHash);

        builder.Property(v => v.ContentHash).IsRequired().HasMaxLength(64);
        builder.Property(v => v.Title).IsRequired().HasMaxLength(500);
        builder.Property(v => v.FileName).IsRequired().HasMaxLength(255);
        builder.Property(v => v.MimeType).IsRequired().HasMaxLength(100);
        builder.Property(v => v.ThumbnailPosterHash).HasMaxLength(64);
        builder.Property(v => v.ExternalPosterHash).HasMaxLength(64);
        builder.Property(v => v.EmbeddedTitle).HasMaxLength(500);
        builder.Property(v => v.EmbeddedImdbId).HasMaxLength(20);
        builder.Property(v => v.EmbeddedDate).HasMaxLength(50);
        builder.Property(v => v.EmbeddedLanguage).HasMaxLength(20);
        builder.Property(v => v.TmdbId);
        builder.Property(v => v.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(v => v.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(v => v.Title).HasDatabaseName("ix_canonical_videos_title");
        builder.HasIndex(v => v.EmbeddedImdbId).HasDatabaseName("ix_canonical_videos_embedded_imdb_id");
        builder.HasIndex(v => v.EmbeddedTmdbId).HasDatabaseName("ix_canonical_videos_embedded_tmdb_id");
        builder.HasIndex(v => v.TmdbId).HasDatabaseName("ix_canonical_videos_tmdb_id");
    }
}
