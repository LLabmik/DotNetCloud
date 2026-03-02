using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DotNetCloud.Core.Data.Entities.Modules;

namespace DotNetCloud.Core.Data.Configuration.Modules;

/// <summary>
/// EF Core configuration for the InstalledModule entity.
/// </summary>
public class InstalledModuleConfiguration : IEntityTypeConfiguration<InstalledModule>
{
    /// <summary>
    /// Configures the InstalledModule entity mapping, constraints, and indexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<InstalledModule> builder)
    {
        // Table naming will be handled by ITableNamingStrategy
        // PostgreSQL: core.installed_modules
        // SQL Server: [core].[InstalledModules]
        // MariaDB: core_installed_modules

        // Primary Key (ModuleId is the natural key, not auto-generated)
        builder.HasKey(m => m.ModuleId);

        // Properties
        builder.Property(m => m.ModuleId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Version)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.InstalledAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(m => m.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes for efficient querying
        builder.HasIndex(m => m.Status)
            .HasDatabaseName("ix_installed_modules_status");

        builder.HasIndex(m => m.InstalledAt)
            .HasDatabaseName("ix_installed_modules_installed_at");

        // Relationships
        builder.HasMany(m => m.CapabilityGrants)
            .WithOne(g => g.Module)
            .HasForeignKey(g => g.ModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_module_capability_grants_module_id");
    }
}
