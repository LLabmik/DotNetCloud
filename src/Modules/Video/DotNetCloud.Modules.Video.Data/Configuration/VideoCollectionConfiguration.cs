using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="VideoCollection"/> entity.
/// </summary>
public sealed class VideoCollectionConfiguration : IEntityTypeConfiguration<VideoCollection>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoCollection> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(c => c.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasIndex(c => c.OwnerId).HasDatabaseName("ix_video_collections_owner_id");
        builder.HasIndex(c => c.Name).HasDatabaseName("ix_video_collections_name");
    }
}
