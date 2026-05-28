using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="UserVideo"/> entity.
/// </summary>
public sealed class UserVideoConfiguration : IEntityTypeConfiguration<UserVideo>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserVideo> builder)
    {
        builder.ToTable("user_videos");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.CanonicalContentHash).IsRequired().HasMaxLength(64);
        builder.Property(v => v.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(v => v.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(v => !v.IsDeleted);

        builder.HasOne(v => v.CanonicalVideo)
            .WithMany(cv => cv.UserVideos)
            .HasForeignKey(v => v.CanonicalContentHash)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.FileNodeId, v.OwnerId }).IsUnique().HasDatabaseName("uq_user_videos_file_node_owner_id");
        builder.HasIndex(v => v.OwnerId).HasDatabaseName("ix_user_videos_owner_id");
        builder.HasIndex(v => v.CanonicalContentHash).HasDatabaseName("ix_user_videos_canonical_content_hash");
        builder.HasIndex(v => new { v.OwnerId, v.CreatedAt }).HasDatabaseName("ix_user_videos_owner_created_at");
        builder.HasIndex(v => v.IsDeleted).HasDatabaseName("ix_user_videos_is_deleted");
    }
}
