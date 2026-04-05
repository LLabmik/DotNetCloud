using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="PlaybackHistory"/> entity.
/// </summary>
public sealed class PlaybackHistoryConfiguration : IEntityTypeConfiguration<PlaybackHistory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlaybackHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.PlayedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(h => h.Track)
            .WithMany()
            .HasForeignKey(h => h.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => new { h.UserId, h.PlayedAt }).HasDatabaseName("ix_playback_history_user_played_at");
        builder.HasIndex(h => h.TrackId).HasDatabaseName("ix_playback_history_track_id");
    }
}
