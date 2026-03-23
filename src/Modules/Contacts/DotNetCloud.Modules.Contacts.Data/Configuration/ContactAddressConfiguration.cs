using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Contacts.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ContactAddress"/> entity.
/// </summary>
public sealed class ContactAddressConfiguration : IEntityTypeConfiguration<ContactAddress>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactAddress> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Label)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Street).HasMaxLength(500);
        builder.Property(a => a.City).HasMaxLength(200);
        builder.Property(a => a.Region).HasMaxLength(200);
        builder.Property(a => a.PostalCode).HasMaxLength(20);
        builder.Property(a => a.Country).HasMaxLength(100);

        builder.HasOne(a => a.Contact)
            .WithMany(c => c.Addresses)
            .HasForeignKey(a => a.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ContactId)
            .HasDatabaseName("ix_contact_addresses_contact_id");
    }
}
