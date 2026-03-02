using DotNetCloud.Core.Data.Entities.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Permissions;

/// <summary>
/// EF Core fluent API configuration for the <see cref="Permission"/> entity.
/// </summary>
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    /// <summary>
    /// Configures the <see cref="Permission"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        // Primary key
        builder.HasKey(p => p.Id);

        // Properties
        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        // Unique constraint on Code
        builder.HasIndex(p => p.Code)
            .IsUnique()
            .HasDatabaseName("IX_permissions_code");

        // Relationships
        builder.HasMany(p => p.RolePermissions)
            .WithOne(rp => rp.Permission)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
