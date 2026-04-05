using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Playlist"/> entity.
/// </summary>
public sealed class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(500);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(p => p.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasIndex(p => p.OwnerId).HasDatabaseName("ix_playlists_owner_id");
        builder.HasIndex(p => p.Name).HasDatabaseName("ix_playlists_name");
        builder.HasIndex(p => p.IsDeleted).HasDatabaseName("ix_playlists_is_deleted");
    }
}
