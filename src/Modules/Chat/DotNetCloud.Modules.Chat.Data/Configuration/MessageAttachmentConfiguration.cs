using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="MessageAttachment"/> entity.
/// </summary>
public sealed class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.MimeType)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.ThumbnailUrl)
            .HasMaxLength(500);

        // FK to Message
        builder.HasOne(a => a.Message)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for cross-module file lookups
        builder.HasIndex(a => a.FileNodeId)
            .HasDatabaseName("ix_chat_message_attachments_file_node_id");
    }
}
