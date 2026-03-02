using DotNetCloud.Core.Data.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Settings;

/// <summary>
/// EF Core fluent API configuration for the <see cref="SystemSetting"/> entity.
/// </summary>
/// <remarks>
/// Configures the System Settings table with:
/// - Composite primary key (Module, Key)
/// - Required fields and constraints
/// - Indexes for query optimization
/// - Table naming strategy application
/// </remarks>
public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    /// <summary>
    /// Configures the <see cref="SystemSetting"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        // Composite primary key: (Module, Key)
        builder.HasKey(s => new { s.Module, s.Key });

        // Properties configuration
        builder.Property(s => s.Module)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("module");

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("key");

        builder.Property(s => s.Value)
            .IsRequired()
            .HasMaxLength(10000)
            .HasColumnName("value");

        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(s => s.Description)
            .HasMaxLength(500)
            .HasColumnName("description");

        // Indexes for query optimization
        builder.HasIndex(s => s.Module)
            .HasDatabaseName("IX_system_settings_module");

        builder.HasIndex(s => s.UpdatedAt)
            .HasDatabaseName("IX_system_settings_updated_at");

        // Table naming will be applied by ITableNamingStrategy during context configuration
        builder.ToTable("SystemSettings");
    }
}
