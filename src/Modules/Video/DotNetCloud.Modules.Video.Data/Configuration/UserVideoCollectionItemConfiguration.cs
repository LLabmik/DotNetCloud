using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Video.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="UserVideoCollectionItem"/> junction entity.
/// </summary>
public sealed class UserVideoCollectionItemConfiguration : IEntityTypeConfiguration<UserVideoCollectionItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserVideoCollectionItem> builder)
    {
        builder.ToTable("user_video_collection_items");
        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.CanonicalContentHash).IsRequired().HasMaxLength(64);
        builder.Property(ci => ci.AddedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(ci => ci.Collection)
            .WithMany(c => c.Items)
            .HasForeignKey(ci => ci.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ci => new { ci.CollectionId, ci.CanonicalContentHash })
            .IsUnique()
            .HasDatabaseName("uq_user_collection_items_collection_hash");

        builder.HasIndex(ci => ci.CollectionId).HasDatabaseName("ix_user_collection_items_collection_id");
        builder.HasIndex(ci => ci.CanonicalContentHash).HasDatabaseName("ix_user_collection_items_canonical_hash");
    }
}
