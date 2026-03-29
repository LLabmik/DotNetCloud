using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="SprintCard"/> join entity.
/// </summary>
public sealed class SprintCardConfiguration : IEntityTypeConfiguration<SprintCard>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SprintCard> builder)
    {
        builder.HasKey(sc => new { sc.SprintId, sc.CardId });

        builder.Property(sc => sc.AddedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(sc => sc.Sprint)
            .WithMany(s => s.SprintCards)
            .HasForeignKey(sc => sc.SprintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sc => sc.Card)
            .WithMany(c => c.SprintCards)
            .HasForeignKey(sc => sc.CardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
