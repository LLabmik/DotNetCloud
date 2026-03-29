using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CardChecklist"/> entity.
/// </summary>
public sealed class CardChecklistConfiguration : IEntityTypeConfiguration<CardChecklist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CardChecklist> builder)
    {
        builder.HasKey(cl => cl.Id);

        builder.Property(cl => cl.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(cl => cl.Position)
            .IsRequired();

        builder.Property(cl => cl.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(cl => cl.Card)
            .WithMany(c => c.Checklists)
            .HasForeignKey(cl => cl.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(cl => new { cl.CardId, cl.Position })
            .HasDatabaseName("ix_card_checklists_card_position");
    }
}
