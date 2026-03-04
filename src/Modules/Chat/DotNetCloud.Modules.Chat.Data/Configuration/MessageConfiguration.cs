using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Message"/> entity.
/// </summary>
public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(m => m.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.SentAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft-delete query filter
        builder.HasQueryFilter(m => !m.IsDeleted);

        // FK to Channel
        builder.HasOne(m => m.Channel)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referencing FK for replies
        builder.HasOne(m => m.ReplyToMessage)
            .WithMany()
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(m => new { m.ChannelId, m.SentAt })
            .HasDatabaseName("ix_chat_messages_channel_sent");

        builder.HasIndex(m => m.SenderUserId)
            .HasDatabaseName("ix_chat_messages_sender");

        builder.HasIndex(m => m.IsDeleted)
            .HasDatabaseName("ix_chat_messages_is_deleted");
    }
}
