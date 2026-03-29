using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="BoardActivity"/> entity.
/// </summary>
public sealed class BoardActivityConfiguration : IEntityTypeConfiguration<BoardActivity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BoardActivity> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Details)
            .HasColumnType("text");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(a => a.Board)
            .WithMany(b => b.Activities)
            .HasForeignKey(a => a.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(a => new { a.BoardId, a.CreatedAt })
            .HasDatabaseName("ix_board_activities_board_created");

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("ix_board_activities_user_id");

        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("ix_board_activities_entity");
    }
}
