using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Genre"/> entity.
/// </summary>
public sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);

        builder.HasIndex(g => g.Name).IsUnique().HasDatabaseName("uq_genres_name");
    }
}
