using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Contacts.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ContactEmail"/> entity.
/// </summary>
public sealed class ContactEmailConfiguration : IEntityTypeConfiguration<ContactEmail>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactEmail> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Address)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(e => e.Label)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(e => e.Contact)
            .WithMany(c => c.Emails)
            .HasForeignKey(e => e.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ContactId)
            .HasDatabaseName("ix_contact_emails_contact_id");

        builder.HasIndex(e => e.Address)
            .HasDatabaseName("ix_contact_emails_address");
    }
}
