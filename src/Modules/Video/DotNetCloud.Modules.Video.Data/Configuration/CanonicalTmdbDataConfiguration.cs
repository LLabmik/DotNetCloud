using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalTmdbData"/> entity.
/// </summary>
public sealed class CanonicalTmdbDataConfiguration : IEntityTypeConfiguration<CanonicalTmdbData>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalTmdbData> builder)
    {
        builder.ToTable("canonical_tmdb_data");
        builder.HasKey(t => t.TmdbId);
        builder.Property(t => t.TmdbId).ValueGeneratedNever();

        builder.Property(t => t.TmdbTitle).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Overview).HasMaxLength(5000);
        builder.Property(t => t.Genres).HasMaxLength(500);
        builder.Property(t => t.ExternalPosterHash).HasMaxLength(64);
        builder.Property(t => t.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(t => t.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
