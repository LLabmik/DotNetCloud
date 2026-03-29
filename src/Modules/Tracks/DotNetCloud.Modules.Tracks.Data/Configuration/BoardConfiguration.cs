using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Board"/> entity.
/// </summary>
public sealed class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Description)
            .HasColumnType("text");

        builder.Property(b => b.Color)
            .HasMaxLength(20);

        builder.Property(b => b.ETag)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(b => b.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(b => b.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft-delete query filter
        builder.HasQueryFilter(b => !b.IsDeleted);

        // Indexes
        builder.HasIndex(b => b.OwnerId)
            .HasDatabaseName("ix_boards_owner_id");

        builder.HasIndex(b => b.IsArchived)
            .HasDatabaseName("ix_boards_is_archived");

        builder.HasIndex(b => b.IsDeleted)
            .HasDatabaseName("ix_boards_is_deleted");

        builder.HasIndex(b => b.CreatedAt)
            .HasDatabaseName("ix_boards_created_at");
    }
}
