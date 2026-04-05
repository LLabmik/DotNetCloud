using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="VideoCollectionItem"/> junction entity.
/// </summary>
public sealed class VideoCollectionItemConfiguration : IEntityTypeConfiguration<VideoCollectionItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoCollectionItem> builder)
    {
        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.AddedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(ci => ci.Collection)
            .WithMany(c => c.Items)
            .HasForeignKey(ci => ci.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ci => ci.Video)
            .WithMany(v => v.CollectionItems)
            .HasForeignKey(ci => ci.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ci => new { ci.CollectionId, ci.VideoId })
            .IsUnique()
            .HasDatabaseName("uq_collection_items_collection_video");

        builder.HasIndex(ci => ci.CollectionId).HasDatabaseName("ix_collection_items_collection_id");
        builder.HasIndex(ci => ci.VideoId).HasDatabaseName("ix_collection_items_video_id");
    }
}
