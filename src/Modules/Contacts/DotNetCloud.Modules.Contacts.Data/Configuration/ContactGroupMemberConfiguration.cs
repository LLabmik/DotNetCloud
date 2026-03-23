using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Contacts.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ContactGroupMember"/> join entity.
/// </summary>
public sealed class ContactGroupMemberConfiguration : IEntityTypeConfiguration<ContactGroupMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactGroupMember> builder)
    {
        builder.HasKey(m => new { m.GroupId, m.ContactId });

        builder.Property(m => m.AddedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(m => m.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Contact)
            .WithMany(c => c.GroupMemberships)
            .HasForeignKey(m => m.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.ContactId)
            .HasDatabaseName("ix_contact_group_members_contact_id");
    }
}
