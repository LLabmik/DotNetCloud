using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="MessageLinkPreview"/> entity.
/// </summary>
public sealed class MessageLinkPreviewConfiguration : IEntityTypeConfiguration<MessageLinkPreview>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageLinkPreview> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Url)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.Title)
            .HasMaxLength(500);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.ImageUrl)
            .HasMaxLength(2000);

        builder.Property(p => p.SiteName)
            .HasMaxLength(200);

        builder.Property(p => p.FaviconUrl)
            .HasMaxLength(2000);

        // 1:0..1 relationship with Message (one preview per message)
        builder.HasOne(p => p.Message)
            .WithOne(m => m.LinkPreview)
            .HasForeignKey<MessageLinkPreview>(p => p.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Enforce a single preview per message
        builder.HasIndex(p => p.MessageId)
            .IsUnique()
            .HasDatabaseName("ix_chat_message_link_previews_message_id");
    }
}
