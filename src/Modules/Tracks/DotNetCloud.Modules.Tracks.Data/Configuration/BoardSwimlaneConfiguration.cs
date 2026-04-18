using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="BoardSwimlane"/> entity.
/// </summary>
public sealed class BoardSwimlaneConfiguration : IEntityTypeConfiguration<BoardSwimlane>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BoardSwimlane> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.Position)
            .IsRequired();

        builder.Property(l => l.Color)
            .HasMaxLength(20);

        builder.Property(l => l.IsDone)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(l => l.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(l => l.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(l => l.Board)
            .WithMany(b => b.Swimlanes)
            .HasForeignKey(l => l.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(l => new { l.BoardId, l.Position })
            .HasDatabaseName("ix_board_swimlanes_board_position");

        builder.HasIndex(l => l.IsArchived)
            .HasDatabaseName("ix_board_swimlanes_is_archived");
    }
}
