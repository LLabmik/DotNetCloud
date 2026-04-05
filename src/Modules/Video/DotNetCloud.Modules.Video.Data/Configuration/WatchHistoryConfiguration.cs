using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="WatchHistory"/> entity.
/// </summary>
public sealed class WatchHistoryConfiguration : IEntityTypeConfiguration<WatchHistory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WatchHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.WatchedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(h => h.Video)
            .WithMany(v => v.WatchHistories)
            .HasForeignKey(h => h.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => h.UserId).HasDatabaseName("ix_watch_history_user_id");
        builder.HasIndex(h => h.VideoId).HasDatabaseName("ix_watch_history_video_id");
        builder.HasIndex(h => new { h.UserId, h.WatchedAt }).HasDatabaseName("ix_watch_history_user_watched_at");
    }
}
