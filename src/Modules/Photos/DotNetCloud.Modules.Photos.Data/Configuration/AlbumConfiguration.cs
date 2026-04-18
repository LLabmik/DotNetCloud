using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Photos.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Album"/> entity.
/// </summary>
public sealed class AlbumConfiguration : IEntityTypeConfiguration<Album>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Album> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Description)
            .HasColumnType("text");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(a => a.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft-delete query filter
        builder.HasQueryFilter(a => !a.IsDeleted);

        // Indexes
        builder.HasIndex(a => a.OwnerId)
            .HasDatabaseName("ix_albums_owner_id");

        builder.HasIndex(a => a.IsDeleted)
            .HasDatabaseName("ix_albums_is_deleted");

        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("ix_albums_created_at");
    }
}
