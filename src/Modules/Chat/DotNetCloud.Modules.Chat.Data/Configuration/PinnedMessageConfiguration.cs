using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="PinnedMessage"/> entity.
/// </summary>
public sealed class PinnedMessageConfiguration : IEntityTypeConfiguration<PinnedMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PinnedMessage> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PinnedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // FK to Channel
        builder.HasOne(p => p.Channel)
            .WithMany(c => c.PinnedMessages)
            .HasForeignKey(p => p.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Message
        builder.HasOne(p => p.Message)
            .WithMany()
            .HasForeignKey(p => p.MessageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique: can't pin the same message twice in a channel
        builder.HasIndex(p => new { p.ChannelId, p.MessageId })
            .IsUnique()
            .HasDatabaseName("ix_chat_pinned_messages_channel_message");
    }
}
