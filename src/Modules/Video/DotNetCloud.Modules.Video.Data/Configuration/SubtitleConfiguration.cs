using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Subtitle"/> entity.
/// </summary>
public sealed class SubtitleConfiguration : IEntityTypeConfiguration<Subtitle>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Subtitle> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Language).IsRequired().HasMaxLength(10);
        builder.Property(s => s.Label).HasMaxLength(100);
        builder.Property(s => s.Format).IsRequired().HasMaxLength(10);
        builder.Property(s => s.Content).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(s => s.Video)
            .WithMany(v => v.Subtitles)
            .HasForeignKey(s => s.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.VideoId).HasDatabaseName("ix_subtitles_video_id");
        builder.HasIndex(s => new { s.VideoId, s.Language }).HasDatabaseName("ix_subtitles_video_language");
    }
}
