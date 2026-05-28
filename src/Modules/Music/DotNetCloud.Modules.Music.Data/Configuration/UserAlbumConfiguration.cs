using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="UserAlbum"/> entity.
/// </summary>
public sealed class UserAlbumConfiguration : IEntityTypeConfiguration<UserAlbum>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserAlbum> builder)
    {
        builder.ToTable("user_albums");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(a => a.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasOne(a => a.CanonicalAlbum)
            .WithMany(ca => ca.UserAlbums)
            .HasForeignKey(a => a.CanonicalAlbumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.OwnerId).HasDatabaseName("ix_user_albums_owner_id");
        builder.HasIndex(a => a.CanonicalAlbumId).HasDatabaseName("ix_user_albums_canonical_album_id");
        builder.HasIndex(a => new { a.OwnerId, a.CanonicalAlbumId }).IsUnique().HasDatabaseName("uq_user_albums_owner_album");
        builder.HasIndex(a => a.IsDeleted).HasDatabaseName("ix_user_albums_is_deleted");
    }
}
