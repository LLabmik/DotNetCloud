using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="SwimlaneTransitionRule"/>.
/// </summary>
public sealed class SwimlaneTransitionRuleConfiguration : IEntityTypeConfiguration<SwimlaneTransitionRule>
{
    public void Configure(EntityTypeBuilder<SwimlaneTransitionRule> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ProductId)
            .IsRequired();

        builder.Property(r => r.FromSwimlaneId)
            .IsRequired();

        builder.Property(r => r.ToSwimlaneId)
            .IsRequired();

        builder.Property(r => r.IsAllowed)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one rule per product + from/to pair
        builder.HasIndex(r => new { r.ProductId, r.FromSwimlaneId, r.ToSwimlaneId })
            .IsUnique()
            .HasDatabaseName("ix_swimlane_transition_rules_product_from_to");

        // Fast lookup by from-swimlane for move validation
        builder.HasIndex(r => new { r.FromSwimlaneId, r.ToSwimlaneId })
            .HasDatabaseName("ix_swimlane_transition_rules_from_to");

        // Navigation
        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.FromSwimlane)
            .WithMany()
            .HasForeignKey(r => r.FromSwimlaneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ToSwimlane)
            .WithMany()
            .HasForeignKey(r => r.ToSwimlaneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
