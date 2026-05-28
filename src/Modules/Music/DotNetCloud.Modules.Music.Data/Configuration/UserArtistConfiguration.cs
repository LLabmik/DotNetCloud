using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="UserArtist"/> entity.
/// </summary>
public sealed class UserArtistConfiguration : IEntityTypeConfiguration<UserArtist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserArtist> builder)
    {
        builder.ToTable("user_artists");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(a => a.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasOne(a => a.CanonicalArtist)
            .WithMany(ca => ca.UserArtists)
            .HasForeignKey(a => a.CanonicalArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.OwnerId).HasDatabaseName("ix_user_artists_owner_id");
        builder.HasIndex(a => a.CanonicalArtistId).HasDatabaseName("ix_user_artists_canonical_artist_id");
        builder.HasIndex(a => new { a.OwnerId, a.CanonicalArtistId }).IsUnique().HasDatabaseName("uq_user_artists_owner_artist");
        builder.HasIndex(a => a.IsDeleted).HasDatabaseName("ix_user_artists_is_deleted");
    }
}
