using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CardDependency"/> entity.
/// </summary>
public sealed class CardDependencyConfiguration : IEntityTypeConfiguration<CardDependency>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CardDependency> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(d => d.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(d => d.Card)
            .WithMany(c => c.Dependencies)
            .HasForeignKey(d => d.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.DependsOnCard)
            .WithMany(c => c.Dependents)
            .HasForeignKey(d => d.DependsOnCardId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique: no duplicate dependency relationships
        builder.HasIndex(d => new { d.CardId, d.DependsOnCardId, d.Type })
            .IsUnique()
            .HasDatabaseName("uq_card_dependencies_card_depends_type");

        builder.HasIndex(d => d.DependsOnCardId)
            .HasDatabaseName("ix_card_dependencies_depends_on");
    }
}
