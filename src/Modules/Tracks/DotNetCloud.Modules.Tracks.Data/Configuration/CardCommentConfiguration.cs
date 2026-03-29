using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CardComment"/> entity.
/// </summary>
public sealed class CardCommentConfiguration : IEntityTypeConfiguration<CardComment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CardComment> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(c => c.Card)
            .WithMany(card => card.Comments)
            .HasForeignKey(c => c.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft-delete query filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Indexes
        builder.HasIndex(c => c.CardId)
            .HasDatabaseName("ix_card_comments_card_id");

        builder.HasIndex(c => c.UserId)
            .HasDatabaseName("ix_card_comments_user_id");

        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("ix_card_comments_created_at");
    }
}
