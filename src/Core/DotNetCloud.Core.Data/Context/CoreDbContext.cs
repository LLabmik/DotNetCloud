using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Configuration.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Configuration.Organizations;
using DotNetCloud.Core.Data.Entities.Permissions;
using DotNetCloud.Core.Data.Configuration.Permissions;

namespace DotNetCloud.Core.Data.Context;

/// <summary>
/// Core database context for DotNetCloud application.
/// Manages all core entities including ASP.NET Core Identity and applies naming strategies based on the database provider.
/// Extends IdentityDbContext to provide ASP.NET Core Identity support with Guid primary keys.
/// </summary>
public class CoreDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
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

    // Organization Hierarchy DbSets
    /// <summary>
    /// Gets or sets the Organizations DbSet.
    /// </summary>
    public DbSet<Organization> Organizations => Set<Organization>();

    /// <summary>
    /// Gets or sets the Teams DbSet.
    /// </summary>
    public DbSet<Team> Teams => Set<Team>();

    /// <summary>
    /// Gets or sets the TeamMembers DbSet.
    /// </summary>
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    /// <summary>
    /// Gets or sets the Groups DbSet.
    /// </summary>
    public DbSet<Group> Groups => Set<Group>();

    /// <summary>
    /// Gets or sets the GroupMembers DbSet.
    /// </summary>
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    /// <summary>
    /// Gets or sets the OrganizationMembers DbSet.
    /// </summary>
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    // Permission System DbSets
    /// <summary>
    /// Gets or sets the Permissions DbSet.
    /// </summary>
    /// <remarks>
    /// Represents all available permissions in the system that can be assigned to roles.
    /// Includes both system-defined and custom permissions.
    /// </remarks>
    public DbSet<Permission> Permissions => Set<Permission>();

    /// <summary>
    /// Gets or sets the Roles DbSet.
    /// </summary>
    /// <remarks>
    /// Represents role definitions that group permissions together.
    /// This is distinct from ASP.NET Core Identity's IdentityRole and represents
    /// application-level role definitions for the DotNetCloud permission system.
    /// </remarks>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>
    /// Gets or sets the RolePermissions DbSet.
    /// </summary>
    /// <remarks>
    /// Represents the many-to-many junction table between Roles and Permissions.
    /// Used to define which permissions are assigned to each role.
    /// </remarks>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

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
        // Apply custom configurations for ApplicationUser and ApplicationRole
        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationRoleConfiguration());

        // Configure Identity tables with naming strategy
        // Note: Identity tables (Users, Roles, UserRoles, UserClaims, UserLogins, UserTokens, RoleClaims, UserPasskeys)
        // will have their names transformed by ApplyNamingStrategy method above
    }

    /// <summary>
    /// Configures organization hierarchy entities.
    /// Includes Organization, Team, TeamMember, Group, GroupMember, and OrganizationMember.
    /// </summary>
    private void ConfigureOrganizationModels(ModelBuilder modelBuilder)
    {
        // Apply configurations for all organization entities
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new TeamConfiguration());
        modelBuilder.ApplyConfiguration(new TeamMemberConfiguration());
        modelBuilder.ApplyConfiguration(new GroupConfiguration());
        modelBuilder.ApplyConfiguration(new GroupMemberConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationMemberConfiguration());
    }

    /// <summary>
    /// Configures permission and role entities.
    /// Includes Permission, Role, and RolePermission.
    /// </summary>
    private void ConfigurePermissionModels(ModelBuilder modelBuilder)
    {
        // Apply configurations for all permission entities
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
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
