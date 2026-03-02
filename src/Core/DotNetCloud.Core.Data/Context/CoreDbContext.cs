using Microsoft.EntityFrameworkCore;
using DotNetCloud.Core.Data.Naming;

namespace DotNetCloud.Core.Data.Context;

/// <summary>
/// Core database context for DotNetCloud application.
/// Manages all core entities and applies naming strategies based on the database provider.
/// </summary>
public class CoreDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Creates a new instance of CoreDbContext.
    /// </summary>
    /// <param name="options">The DbContext options</param>
    /// <param name="namingStrategy">The naming strategy for the configured database provider</param>
    public CoreDbContext(DbContextOptions<CoreDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(namingStrategy);
        _namingStrategy = namingStrategy;
    }

    /// <summary>
    /// Gets the naming strategy used by this context.
    /// </summary>
    public ITableNamingStrategy NamingStrategy => _namingStrategy;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure naming strategy for all entities
        ApplyNamingStrategy(modelBuilder);

        // Configure specific entity models
        ConfigureIdentityModels(modelBuilder);
        ConfigureOrganizationModels(modelBuilder);
        ConfigurePermissionModels(modelBuilder);
        ConfigureSettingModels(modelBuilder);
        ConfigureDeviceModels(modelBuilder);
        ConfigureModuleModels(modelBuilder);
    }

    /// <summary>
    /// Applies the naming strategy to all entity configurations.
    /// This method dynamically configures table names, column names, and constraint names
    /// based on the active naming strategy (PostgreSQL, SQL Server, or MariaDB).
    /// </summary>
    private void ApplyNamingStrategy(ModelBuilder modelBuilder)
    {
        // Get the module name from the schema if using PostgreSQL/SQL Server
        var moduleName = "core";

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var entityName = entity.Name.Split('.').Last();

            // Set the schema for PostgreSQL and SQL Server
            var schema = _namingStrategy.GetSchemaForModule(moduleName);
            if (schema != null)
            {
                entity.SetSchema(schema);
            }

            // Set the table name using naming strategy
            entity.SetTableName(_namingStrategy.GetTableName(entityName, moduleName));

            // Configure all properties with naming strategy
            foreach (var property in entity.GetProperties())
            {
                var columnName = _namingStrategy.GetColumnName(property.Name);
                property.SetColumnName(columnName);
            }
        }
    }

    /// <summary>
    /// Configures ASP.NET Core Identity entities.
    /// Includes ApplicationUser, ApplicationRole, and their relationships.
    /// </summary>
    private void ConfigureIdentityModels(ModelBuilder modelBuilder)
    {
        // Placeholder for Identity model configuration
        // Will be implemented when Identity entities are created
    }

    /// <summary>
    /// Configures organization hierarchy entities.
    /// Includes Organization, Team, TeamMember, Group, GroupMember, and OrganizationMember.
    /// </summary>
    private void ConfigureOrganizationModels(ModelBuilder modelBuilder)
    {
        // Placeholder for Organization model configuration
        // Will be implemented when Organization entities are created
    }

    /// <summary>
    /// Configures permission and role entities.
    /// Includes Permission, Role, and RolePermission.
    /// </summary>
    private void ConfigurePermissionModels(ModelBuilder modelBuilder)
    {
        // Placeholder for Permission model configuration
        // Will be implemented when Permission entities are created
    }

    /// <summary>
    /// Configures setting entities.
    /// Includes SystemSetting, OrganizationSetting, and UserSetting.
    /// </summary>
    private void ConfigureSettingModels(ModelBuilder modelBuilder)
    {
        // Placeholder for Settings model configuration
        // Will be implemented when Setting entities are created
    }

    /// <summary>
    /// Configures device and session entities.
    /// Includes UserDevice for tracking user devices.
    /// </summary>
    private void ConfigureDeviceModels(ModelBuilder modelBuilder)
    {
        // Placeholder for Device model configuration
        // Will be implemented when Device entities are created
    }

    /// <summary>
    /// Configures module registry and capability grant entities.
    /// Includes InstalledModule and ModuleCapabilityGrant.
    /// </summary>
    private void ConfigureModuleModels(ModelBuilder modelBuilder)
    {
        // Placeholder for Module model configuration
        // Will be implemented when Module entities are created
    }
}
