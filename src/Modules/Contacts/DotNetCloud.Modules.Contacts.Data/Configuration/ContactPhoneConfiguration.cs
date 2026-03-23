using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Contacts.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ContactPhone"/> entity.
/// </summary>
public sealed class ContactPhoneConfiguration : IEntityTypeConfiguration<ContactPhone>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactPhone> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Number)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Label)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(p => p.Contact)
            .WithMany(c => c.PhoneNumbers)
            .HasForeignKey(p => p.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.ContactId)
            .HasDatabaseName("ix_contact_phones_contact_id");
    }
}
