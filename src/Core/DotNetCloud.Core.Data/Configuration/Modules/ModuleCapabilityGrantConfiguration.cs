using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DotNetCloud.Core.Data.Entities.Modules;

namespace DotNetCloud.Core.Data.Configuration.Modules;

/// <summary>
/// EF Core configuration for the ModuleCapabilityGrant entity.
/// </summary>
public class ModuleCapabilityGrantConfiguration : IEntityTypeConfiguration<ModuleCapabilityGrant>
{
    /// <summary>
    /// Configures the ModuleCapabilityGrant entity mapping, constraints, and indexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<ModuleCapabilityGrant> builder)
    {
        // Table naming will be handled by ITableNamingStrategy
        // PostgreSQL: core.module_capability_grants
        // SQL Server: [core].[ModuleCapabilityGrants]
        // MariaDB: core_module_capability_grants

        // Primary Key
        builder.HasKey(g => g.Id);

        // Properties
        builder.Property(g => g.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(g => g.ModuleId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.CapabilityName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.GrantedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(g => g.GrantedByUserId)
            .IsRequired(false); // Nullable (null = system-granted)

        // Unique Constraint: One grant per capability per module
        builder.HasIndex(g => new { g.ModuleId, g.CapabilityName })
            .IsUnique()
            .HasDatabaseName("uq_module_capability_grants_module_id_capability_name");

        // Indexes for efficient querying
        builder.HasIndex(g => g.ModuleId)
            .HasDatabaseName("ix_module_capability_grants_module_id");

        builder.HasIndex(g => g.CapabilityName)
            .HasDatabaseName("ix_module_capability_grants_capability_name");

        builder.HasIndex(g => g.GrantedByUserId)
            .HasDatabaseName("ix_module_capability_grants_granted_by_user_id");

        // Relationships
        builder.HasOne(g => g.Module)
            .WithMany(m => m.CapabilityGrants)
            .HasForeignKey(g => g.ModuleId)
            .OnDelete(DeleteBehavior.Cascade) // Delete grants when module is uninstalled
            .HasConstraintName("fk_module_capability_grants_module_id");

        builder.HasOne(g => g.GrantedByUser)
            .WithMany()
            .HasForeignKey(g => g.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict) // Preserve audit trail if admin is deleted
            .HasConstraintName("fk_module_capability_grants_granted_by_user_id");
    }
}
