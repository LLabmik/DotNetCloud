using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Organizations;

/// <summary>
/// EF Core fluent API configuration for the <see cref="Group"/> entity.
/// </summary>
public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    /// <summary>
    /// Configures the <see cref="Group"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        // Primary key
        builder.HasKey(g => g.Id);

        // Properties
        builder.Property(g => g.OrganizationId)
            .IsRequired();

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.Description)
            .HasMaxLength(1000);

        builder.Property(g => g.IsAllUsersGroup)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(g => g.CreatedAt)
            .IsRequired();

        builder.Property(g => g.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(g => g.DeletedAt);

        // Indexes
        builder.HasIndex(g => new { g.OrganizationId, g.Name })
            .IsUnique()
            .HasDatabaseName("IX_groups_org_name");

        builder.HasIndex(g => g.IsDeleted)
            .HasDatabaseName("IX_groups_is_deleted");

        builder.HasIndex(g => g.CreatedAt)
            .HasDatabaseName("IX_groups_created_at");

        builder.HasIndex(g => new { g.OrganizationId, g.IsAllUsersGroup })
            .HasDatabaseName("IX_groups_org_all_users");

        // Soft-delete query filter
        builder.HasQueryFilter(g => !g.IsDeleted);

        // Relationships
        builder.HasOne(g => g.Organization)
            .WithMany(o => o.Groups)
            .HasForeignKey(g => g.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(g => g.Members)
            .WithOne(gm => gm.Group)
            .HasForeignKey(gm => gm.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
