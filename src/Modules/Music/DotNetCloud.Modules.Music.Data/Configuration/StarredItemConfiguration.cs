using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="StarredItem"/> entity.
/// </summary>
public sealed class StarredItemConfiguration : IEntityTypeConfiguration<StarredItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StarredItem> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.StarredAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(s => new { s.UserId, s.ItemType, s.ItemId })
            .IsUnique()
            .HasDatabaseName("uq_starred_items_user_type_item");

        builder.HasIndex(s => s.UserId).HasDatabaseName("ix_starred_items_user_id");
    }
}
