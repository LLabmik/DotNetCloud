using DotNetCloud.Core.Data.Entities.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Permissions;

/// <summary>
/// EF Core fluent API configuration for the <see cref="RolePermission"/> junction entity.
/// </summary>
public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    /// <summary>
    /// Configures the <see cref="RolePermission"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        // Composite primary key
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId })
            .HasName("PK_role_permissions");

        // Foreign keys
        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .HasConstraintName("FK_role_permissions_role_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .HasConstraintName("FK_role_permissions_permission_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for efficient queries
        builder.HasIndex(rp => rp.RoleId)
            .HasDatabaseName("IX_role_permissions_role_id");

        builder.HasIndex(rp => rp.PermissionId)
            .HasDatabaseName("IX_role_permissions_permission_id");
    }
}
