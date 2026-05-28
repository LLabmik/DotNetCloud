using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CanonicalGenre"/> entity.
/// </summary>
public sealed class CanonicalGenreConfiguration : IEntityTypeConfiguration<CanonicalGenre>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanonicalGenre> builder)
    {
        builder.ToTable("canonical_genres");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);

        builder.HasIndex(g => g.Name).IsUnique().HasDatabaseName("uq_canonical_genres_name");
    }
}
