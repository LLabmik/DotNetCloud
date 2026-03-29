using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CardAssignment"/> entity.
/// </summary>
public sealed class CardAssignmentConfiguration : IEntityTypeConfiguration<CardAssignment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CardAssignment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AssignedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(a => a.Card)
            .WithMany(c => c.Assignments)
            .HasForeignKey(a => a.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: one assignment per user per card
        builder.HasIndex(a => new { a.CardId, a.UserId })
            .IsUnique()
            .HasDatabaseName("uq_card_assignments_card_user");

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("ix_card_assignments_user_id");
    }
}
