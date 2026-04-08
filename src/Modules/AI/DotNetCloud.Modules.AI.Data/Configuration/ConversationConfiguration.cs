using DotNetCloud.Modules.AI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.AI.Data.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="Conversation"/>.
/// </summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.Model)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.SystemPrompt)
            .HasColumnType("text");

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft-delete query filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Relationships
        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(c => c.OwnerId)
            .HasDatabaseName("ix_ai_conversations_owner_id");

        builder.HasIndex(c => new { c.OwnerId, c.UpdatedAt })
            .HasDatabaseName("ix_ai_conversations_owner_updated");

        builder.HasIndex(c => c.IsDeleted)
            .HasDatabaseName("ix_ai_conversations_is_deleted");
    }
}
