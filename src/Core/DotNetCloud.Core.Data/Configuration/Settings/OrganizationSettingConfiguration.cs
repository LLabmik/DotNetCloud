using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Settings;

/// <summary>
/// EF Core fluent API configuration for the <see cref="OrganizationSetting"/> entity.
/// </summary>
/// <remarks>
/// Configures the Organization Settings table with:
/// - Primary key on Id
/// - Composite unique constraint (OrganizationId, Module, Key)
/// - Foreign key to Organization with cascade delete
/// - Required fields and constraints
/// - Indexes for query optimization
/// - Table naming strategy application
/// </remarks>
public class OrganizationSettingConfiguration : IEntityTypeConfiguration<OrganizationSetting>
{
    /// <summary>
    /// Configures the <see cref="OrganizationSetting"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<OrganizationSetting> builder)
    {
        // Primary key
        builder.HasKey(s => s.Id);

        // Properties configuration
        builder.Property(s => s.OrganizationId)
            .IsRequired()
            .HasColumnName("organization_id");

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("key");

        builder.Property(s => s.Value)
            .IsRequired()
            .HasMaxLength(10000)
            .HasColumnName("value");

        builder.Property(s => s.Module)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("module");

        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(s => s.Description)
            .HasMaxLength(500)
            .HasColumnName("description");

        // Unique constraint: each organization can have only one setting per (Module, Key)
        builder.HasIndex(s => new { s.OrganizationId, s.Module, s.Key })
            .IsUnique()
            .HasDatabaseName("IX_organization_settings_unique");

        // Indexes for query optimization
        builder.HasIndex(s => s.OrganizationId)
            .HasDatabaseName("IX_organization_settings_organization_id");

        builder.HasIndex(s => s.Module)
            .HasDatabaseName("IX_organization_settings_module");

        builder.HasIndex(s => s.UpdatedAt)
            .HasDatabaseName("IX_organization_settings_updated_at");

        // Foreign key relationship
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_organization_settings_organization_id");

        // Table naming will be applied by ITableNamingStrategy during context configuration
        builder.ToTable("OrganizationSettings");
    }
}
