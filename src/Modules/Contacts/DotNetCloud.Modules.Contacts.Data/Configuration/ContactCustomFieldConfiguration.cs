using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Contacts.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ContactCustomField"/> entity.
/// </summary>
public sealed class ContactCustomFieldConfiguration : IEntityTypeConfiguration<ContactCustomField>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactCustomField> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.Value)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasOne(f => f.Contact)
            .WithMany(c => c.CustomFields)
            .HasForeignKey(f => f.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.ContactId)
            .HasDatabaseName("ix_contact_custom_fields_contact_id");

        builder.HasIndex(f => new { f.ContactId, f.Key })
            .IsUnique()
            .HasDatabaseName("uq_contact_custom_fields_contact_key");
    }
}
