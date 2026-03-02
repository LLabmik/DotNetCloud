using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Identity;

/// <summary>
/// Entity Framework configuration for the ApplicationRole entity.
/// Configures properties, indexes, and relationships.
/// </summary>
public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        // Description optional with max length
        builder.Property(r => r.Description)
            .HasMaxLength(500);

        // IsSystemRole required with default value
        builder.Property(r => r.IsSystemRole)
            .IsRequired()
            .HasDefaultValue(false);

        // Index for system roles
        builder.HasIndex(r => r.IsSystemRole)
            .HasDatabaseName("IX_ApplicationRoles_IsSystemRole");

        // Index for role name (already created by Identity, but we can customize)
        builder.HasIndex(r => r.Name)
            .HasDatabaseName("IX_ApplicationRoles_Name");
    }
}
