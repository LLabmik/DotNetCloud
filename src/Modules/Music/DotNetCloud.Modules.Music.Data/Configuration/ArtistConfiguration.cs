using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Artist"/> entity.
/// </summary>
public sealed class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(500);
        builder.Property(a => a.SortName).HasMaxLength(500);
        builder.Property(a => a.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(a => a.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasIndex(a => a.OwnerId).HasDatabaseName("ix_artists_owner_id");
        builder.HasIndex(a => a.Name).HasDatabaseName("ix_artists_name");
        builder.HasIndex(a => new { a.OwnerId, a.Name }).IsUnique().HasDatabaseName("uq_artists_owner_name");
        builder.HasIndex(a => a.IsDeleted).HasDatabaseName("ix_artists_is_deleted");
    }
}
