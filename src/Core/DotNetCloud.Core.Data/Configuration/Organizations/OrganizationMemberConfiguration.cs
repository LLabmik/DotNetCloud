using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace DotNetCloud.Core.Data.Configuration.Organizations;

/// <summary>
/// EF Core fluent API configuration for the <see cref="OrganizationMember"/> entity.
/// </summary>
public class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    /// <summary>
    /// Configures the <see cref="OrganizationMember"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        // Composite primary key
        builder.HasKey(om => new { om.OrganizationId, om.UserId });

        // Properties
        builder.Property(om => om.OrganizationId)
            .IsRequired();

        builder.Property(om => om.UserId)
            .IsRequired();

        builder.Property(om => om.RoleIds)
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>())
            .HasDefaultValue(new List<Guid>());

        builder.Property(om => om.JoinedAt)
            .IsRequired();

        builder.Property(om => om.InvitedByUserId);

        builder.Property(om => om.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Indexes
        builder.HasIndex(om => om.UserId)
            .HasDatabaseName("IX_org_members_user_id");

        builder.HasIndex(om => om.JoinedAt)
            .HasDatabaseName("IX_org_members_joined_at");

        builder.HasIndex(om => om.IsActive)
            .HasDatabaseName("IX_org_members_is_active");

        builder.HasIndex(om => om.InvitedByUserId)
            .HasDatabaseName("IX_org_members_invited_by");

        // Relationships
        builder.HasOne(om => om.Organization)
            .WithMany(o => o.Members)
            .HasForeignKey(om => om.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(om => om.User)
            .WithMany()
            .HasForeignKey(om => om.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(om => om.InvitedByUser)
            .WithMany()
            .HasForeignKey(om => om.InvitedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
