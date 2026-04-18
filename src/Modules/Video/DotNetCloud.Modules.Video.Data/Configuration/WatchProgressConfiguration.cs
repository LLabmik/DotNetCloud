using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="WatchProgress"/> entity.
/// </summary>
public sealed class WatchProgressConfiguration : IEntityTypeConfiguration<WatchProgress>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WatchProgress> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(p => p.Video)
            .WithMany(v => v.WatchProgresses)
            .HasForeignKey(p => p.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.UserId, p.VideoId })
            .IsUnique()
            .HasDatabaseName("uq_watch_progress_user_video");

        builder.HasIndex(p => p.UserId).HasDatabaseName("ix_watch_progress_user_id");
    }
}
