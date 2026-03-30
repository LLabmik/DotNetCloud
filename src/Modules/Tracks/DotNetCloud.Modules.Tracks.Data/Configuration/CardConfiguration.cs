using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Card"/> entity.
/// </summary>
public sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CardNumber)
            .IsRequired();

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.Description)
            .HasColumnType("text");

        builder.Property(c => c.Position)
            .IsRequired();

        builder.Property(c => c.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.ETag)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(c => c.Swimlane)
            .WithMany(l => l.Cards)
            .HasForeignKey(c => c.SwimlaneId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft-delete query filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Indexes
        builder.HasIndex(c => new { c.SwimlaneId, c.Position })
            .HasDatabaseName("ix_cards_swimlane_position");

        builder.HasIndex(c => c.CreatedByUserId)
            .HasDatabaseName("ix_cards_created_by");

        builder.HasIndex(c => c.DueDate)
            .HasDatabaseName("ix_cards_due_date")
            .HasFilter("\"DueDate\" IS NOT NULL");

        builder.HasIndex(c => c.Priority)
            .HasDatabaseName("ix_cards_priority");

        builder.HasIndex(c => c.IsArchived)
            .HasDatabaseName("ix_cards_is_archived");

        builder.HasIndex(c => c.IsDeleted)
            .HasDatabaseName("ix_cards_is_deleted");

        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("ix_cards_created_at");

        builder.HasIndex(c => c.CardNumber)
            .IsUnique()
            .HasDatabaseName("ix_cards_card_number");
    }
}
