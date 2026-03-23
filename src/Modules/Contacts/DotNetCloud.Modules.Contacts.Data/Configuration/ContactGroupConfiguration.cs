using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Contacts.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ContactGroup"/> entity.
/// </summary>
public sealed class ContactGroupConfiguration : IEntityTypeConfiguration<ContactGroup>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactGroup> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(g => g.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft-delete query filter
        builder.HasQueryFilter(g => !g.IsDeleted);

        // Indexes
        builder.HasIndex(g => g.OwnerId)
            .HasDatabaseName("ix_contact_groups_owner_id");

        builder.HasIndex(g => new { g.OwnerId, g.Name })
            .IsUnique()
            .HasDatabaseName("uq_contact_groups_owner_name")
            .HasFilter(null);
    }
}
