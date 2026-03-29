using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Label"/> entity.
/// </summary>
public sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.Color)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(l => l.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(l => l.Board)
            .WithMany(b => b.Labels)
            .HasForeignKey(l => l.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique label title per board
        builder.HasIndex(l => new { l.BoardId, l.Title })
            .IsUnique()
            .HasDatabaseName("uq_labels_board_title");
    }
}
