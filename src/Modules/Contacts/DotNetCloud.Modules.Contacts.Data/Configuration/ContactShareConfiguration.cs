using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Contacts.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ContactShare"/> entity.
/// </summary>
public sealed class ContactShareConfiguration : IEntityTypeConfiguration<ContactShare>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactShare> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Permission)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(s => s.Contact)
            .WithMany(c => c.Shares)
            .HasForeignKey(s => s.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => s.ContactId)
            .HasDatabaseName("ix_contact_shares_contact_id");

        builder.HasIndex(s => s.SharedWithUserId)
            .HasDatabaseName("ix_contact_shares_shared_with_user");

        builder.HasIndex(s => s.SharedWithTeamId)
            .HasDatabaseName("ix_contact_shares_shared_with_team");
    }
}
