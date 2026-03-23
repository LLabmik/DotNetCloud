using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Contacts.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Contact"/> entity.
/// </summary>
public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.DisplayName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(c => c.ContactType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.FirstName).HasMaxLength(150);
        builder.Property(c => c.LastName).HasMaxLength(150);
        builder.Property(c => c.MiddleName).HasMaxLength(100);
        builder.Property(c => c.Prefix).HasMaxLength(30);
        builder.Property(c => c.Suffix).HasMaxLength(30);
        builder.Property(c => c.PhoneticName).HasMaxLength(300);
        builder.Property(c => c.Nickname).HasMaxLength(150);
        builder.Property(c => c.Organization).HasMaxLength(300);
        builder.Property(c => c.Department).HasMaxLength(200);
        builder.Property(c => c.JobTitle).HasMaxLength(200);
        builder.Property(c => c.AvatarUrl).HasMaxLength(500);
        builder.Property(c => c.Notes).HasMaxLength(10000);
        builder.Property(c => c.WebsiteUrl).HasMaxLength(500);

        builder.Property(c => c.ETag)
            .IsRequired()
            .HasMaxLength(64)
            .IsConcurrencyToken();

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.CreatedByUserId);
        builder.Property(c => c.UpdatedByUserId);

        // Soft-delete query filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Indexes
        builder.HasIndex(c => c.OwnerId)
            .HasDatabaseName("ix_contacts_owner_id");

        builder.HasIndex(c => c.DisplayName)
            .HasDatabaseName("ix_contacts_display_name");

        builder.HasIndex(c => new { c.OwnerId, c.DisplayName })
            .HasDatabaseName("ix_contacts_owner_display_name");

        builder.HasIndex(c => c.ContactType)
            .HasDatabaseName("ix_contacts_type");

        builder.HasIndex(c => c.IsDeleted)
            .HasDatabaseName("ix_contacts_is_deleted");

        builder.HasIndex(c => new { c.OwnerId, c.LastName, c.FirstName })
            .HasDatabaseName("ix_contacts_owner_name");
    }
}
