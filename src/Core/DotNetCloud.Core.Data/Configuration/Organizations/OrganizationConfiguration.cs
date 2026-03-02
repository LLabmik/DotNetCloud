using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Organizations;

/// <summary>
/// EF Core fluent API configuration for the <see cref="Organization"/> entity.
/// </summary>
public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    /// <summary>
    /// Configures the <see cref="Organization"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        // Primary key
        builder.HasKey(o => o.Id);

        // Properties
        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.Description)
            .HasMaxLength(1000);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(o => o.DeletedAt);

        // Indexes
        builder.HasIndex(o => o.Name)
            .IsUnique()
            .HasDatabaseName("IX_organizations_name");

        builder.HasIndex(o => o.IsDeleted)
            .HasDatabaseName("IX_organizations_is_deleted");

        builder.HasIndex(o => o.CreatedAt)
            .HasDatabaseName("IX_organizations_created_at");

        // Soft-delete query filter
        builder.HasQueryFilter(o => !o.IsDeleted);

        // Relationships
        builder.HasMany(o => o.Teams)
            .WithOne(t => t.Organization)
            .HasForeignKey(t => t.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Groups)
            .WithOne(g => g.Organization)
            .HasForeignKey(g => g.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Members)
            .WithOne(om => om.Organization)
            .HasForeignKey(om => om.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
