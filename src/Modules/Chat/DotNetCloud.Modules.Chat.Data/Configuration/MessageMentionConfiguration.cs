using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="MessageMention"/> entity.
/// </summary>
public sealed class MessageMentionConfiguration : IEntityTypeConfiguration<MessageMention>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageMention> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);

        // FK to Message
        builder.HasOne(m => m.Message)
            .WithMany(msg => msg.Mentions)
            .HasForeignKey(m => m.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for looking up all mentions of a specific user
        builder.HasIndex(m => m.MentionedUserId)
            .HasDatabaseName("ix_chat_message_mentions_mentioned_user_id");
    }
}
