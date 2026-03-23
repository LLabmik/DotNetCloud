using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Contacts.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ContactAttachment"/> entity.
/// </summary>
public sealed class ContactAttachmentConfiguration : IEntityTypeConfiguration<ContactAttachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactAttachment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.StoragePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Description)
            .HasMaxLength(500);

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(a => a.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(a => a.Contact)
            .WithMany(c => c.Attachments)
            .HasForeignKey(a => a.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ContactId)
            .HasDatabaseName("ix_contact_attachments_contact_id");

        builder.HasIndex(a => new { a.ContactId, a.IsAvatar })
            .HasDatabaseName("ix_contact_attachments_contact_avatar");
    }
}
