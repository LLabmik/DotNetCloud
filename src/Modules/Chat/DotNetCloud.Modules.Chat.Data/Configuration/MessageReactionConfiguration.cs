using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="MessageReaction"/> entity.
/// </summary>
public sealed class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Emoji)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.ReactedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Composite unique index: one reaction per user per emoji per message
        builder.HasIndex(r => new { r.MessageId, r.UserId, r.Emoji })
            .IsUnique()
            .HasDatabaseName("ix_chat_message_reactions_message_user_emoji");

        // FK to Message
        builder.HasOne(r => r.Message)
            .WithMany(m => m.Reactions)
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
