using DotNetCloud.Modules.Email.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Email.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EmailAttachment"/> entity.
/// </summary>
public sealed class EmailAttachmentConfiguration : IEntityTypeConfiguration<EmailAttachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailAttachment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(a => a.ContentId).HasMaxLength(200);
        builder.Property(a => a.StorageKey).HasMaxLength(500);
        builder.Property(a => a.ContentHash).HasMaxLength(64);
        builder.Property(a => a.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(a => a.Message)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.MessageId).HasDatabaseName("ix_email_attachments_message_id");
    }
}
