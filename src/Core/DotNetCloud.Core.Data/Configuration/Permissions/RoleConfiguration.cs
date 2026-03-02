using DotNetCloud.Core.Data.Entities.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Permissions;

/// <summary>
/// EF Core fluent API configuration for the <see cref="Role"/> entity.
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>
    /// Configures the <see cref="Role"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // Primary key
        builder.HasKey(r => r.Id);

        // Properties
        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .HasMaxLength(1000);

        builder.Property(r => r.IsSystemRole)
            .IsRequired()
            .HasDefaultValue(false);

        // Unique constraint on Name
        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("IX_roles_name");

        // Index on IsSystemRole for filtering
        builder.HasIndex(r => r.IsSystemRole)
            .HasDatabaseName("IX_roles_is_system_role");

        // Relationships
        builder.HasMany(r => r.RolePermissions)
            .WithOne(rp => rp.Role)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
