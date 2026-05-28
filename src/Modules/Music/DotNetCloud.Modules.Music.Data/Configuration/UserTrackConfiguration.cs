using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="UserTrack"/> entity.
/// </summary>
public sealed class UserTrackConfiguration : IEntityTypeConfiguration<UserTrack>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserTrack> builder)
    {
        builder.ToTable("user_tracks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.CanonicalTrackHash).IsRequired().HasMaxLength(64);
        builder.Property(t => t.ContentHash).IsRequired().HasMaxLength(64);
        builder.Property(t => t.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(t => t.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasOne(t => t.CanonicalTrack)
            .WithMany(ct => ct.UserTracks)
            .HasForeignKey(t => t.CanonicalTrackHash)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.CanonicalAlbum)
            .WithMany() // CanonicalAlbum has UserAlbums (via UserAlbum), not direct UserTracks
            .HasForeignKey(t => t.CanonicalAlbumId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.FileNodeId, t.OwnerId }).IsUnique().HasDatabaseName("uq_user_tracks_file_node_owner_id");
        builder.HasIndex(t => t.OwnerId).HasDatabaseName("ix_user_tracks_owner_id");
        builder.HasIndex(t => t.CanonicalTrackHash).HasDatabaseName("ix_user_tracks_canonical_track_hash");
        builder.HasIndex(t => new { t.OwnerId, t.CreatedAt }).HasDatabaseName("ix_user_tracks_owner_created_at");
        builder.HasIndex(t => t.IsDeleted).HasDatabaseName("ix_user_tracks_is_deleted");
        builder.HasIndex(t => t.ContentHash).HasDatabaseName("ix_user_tracks_content_hash");
    }
}
