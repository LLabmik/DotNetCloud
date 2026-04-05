using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="VideoShare"/> entity.
/// </summary>
public sealed class VideoShareConfiguration : IEntityTypeConfiguration<VideoShare>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoShare> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Permission).IsRequired().HasMaxLength(20);
        builder.Property(s => s.ShareToken).HasMaxLength(128);
        builder.Property(s => s.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(s => s.Video)
            .WithMany(v => v.Shares)
            .HasForeignKey(s => s.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.VideoId).HasDatabaseName("ix_video_shares_video_id");
        builder.HasIndex(s => s.SharedByUserId).HasDatabaseName("ix_video_shares_shared_by");
        builder.HasIndex(s => s.SharedWithUserId).HasDatabaseName("ix_video_shares_shared_with");
        builder.HasIndex(s => s.ShareToken).HasDatabaseName("ix_video_shares_token");
    }
}
