using DotNetCloud.Core.Data.Entities.Auth;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Permissions;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Configuration.Auth;
using DotNetCloud.Core.Data.Configuration.Identity;
using DotNetCloud.Core.Data.Configuration.Modules;
using DotNetCloud.Core.Data.Configuration.Organizations;
using DotNetCloud.Core.Data.Configuration.Permissions;
using DotNetCloud.Core.Data.Configuration.Settings;
using DotNetCloud.Core.Data.Infrastructure;
using DotNetCloud.Core.Data.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Context;

/// <summary>
/// Main database context for the DotNetCloud platform.
/// </summary>
/// <remarks>
/// This DbContext serves as the main data access layer for the entire DotNetCloud platform.
/// It extends IdentityDbContext to include ASP.NET Core Identity support with custom user
/// and role types using Guid as the primary key type.
///
/// <para>
/// <b>Entity Groups:</b>
/// <list type="bullet">
/// <item>Identity: ApplicationUser, ApplicationRole (ASP.NET Core Identity)</item>
/// <item>Organizations: Organization, Team, TeamMember, Group, GroupMember, OrganizationMember</item>
/// <item>Permissions: Permission, Role, RolePermission</item>
/// <item>Settings: SystemSetting, OrganizationSetting, UserSetting</item>
/// <item>Devices: UserDevice</item>
/// <item>Modules: InstalledModule, ModuleCapabilityGrant</item>
/// <item>Authentication: OpenIddictApplication, OpenIddictAuthorization, OpenIddictToken, OpenIddictScope</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Multi-Database Support:</b>
/// <list type="bullet">
/// <item>PostgreSQL: Uses schemas (core.*, files.*) and snake_case naming</item>
/// <item>SQL Server: Uses schemas ([core], [files]) and PascalCase naming</item>
/// <item>MariaDB: Uses table prefixes (core_*, files_*) and snake_case naming</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Automatic Features:</b>
/// <list type="bullet">
/// <item>Timestamp management: CreatedAt, UpdatedAt automatically set via TimestampInterceptor</item>
/// <item>Soft-delete: Query filters applied for Organization, Team, Group entities</item>
/// <item>Concurrency: ConcurrencyToken properties configured for optimistic concurrency</item>
/// </list>
/// </para>
/// </remarks>
public class CoreDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoreDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by the DbContext.</param>
    /// <param name="namingStrategy">The naming strategy for table and column names.</param>
    public CoreDbContext(DbContextOptions<CoreDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy ?? throw new ArgumentNullException(nameof(namingStrategy));
    }

    /// <summary>
    /// Gets the table naming strategy used by this context.
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
    public new DbSet<Role> Roles => Set<Role>();

    /// <summary>
    /// Gets or sets the RolePermissions DbSet.
    /// </summary>
    /// <remarks>
    /// Represents the many-to-many junction table between Roles and Permissions.
    /// Used to define which permissions are assigned to each role.
    /// </remarks>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // Settings DbSets
    /// <summary>
    /// Gets or sets the SystemSettings DbSet.
    /// </summary>
    /// <remarks>
    /// Represents system-wide settings that apply across the entire DotNetCloud instance.
    /// System settings have a composite primary key (Module, Key) for efficient configuration management.
    /// </remarks>
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    /// <summary>
    /// Gets or sets the OrganizationSettings DbSet.
    /// </summary>
    /// <remarks>
    /// Represents organization-scoped settings that can override system settings.
    /// Enables per-organization configuration for multi-tenant deployments.
    /// </remarks>
    public DbSet<OrganizationSetting> OrganizationSettings => Set<OrganizationSetting>();

    /// <summary>
    /// Gets or sets the UserSettings DbSet.
    /// </summary>
    /// <remarks>
    /// Represents user-scoped settings for personal preferences and configuration.
    /// Enables per-user customization of the application.
    /// Some settings may contain encrypted sensitive data.
    /// </remarks>
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();

    // Device Registry DbSets
    /// <summary>
    /// Gets or sets the UserDevices DbSet.
    /// </summary>
    /// <remarks>
    /// Represents devices registered by users for accessing the DotNetCloud platform.
    /// Tracks device information, push notification tokens, and last activity.
    /// Used for device management, security monitoring, and presence tracking.
    /// </remarks>
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();

    // Module Registry DbSets
    /// <summary>
    /// Gets or sets the InstalledModules DbSet.
    /// </summary>
    /// <remarks>
    /// Represents modules installed in the DotNetCloud system.
    /// Tracks module versions, status (Enabled/Disabled/UpdateAvailable), and installation metadata.
    /// Used for module lifecycle management and update notifications.
    /// </remarks>
    public DbSet<InstalledModule> InstalledModules => Set<InstalledModule>();

    /// <summary>
    /// Gets or sets the ModuleCapabilityGrants DbSet.
    /// </summary>
    /// <remarks>
    /// Represents capability grants to installed modules.
    /// Tracks which capabilities (IUserDirectory, IStorageProvider, etc.) are granted to modules and when.
    /// Enables capability-based security at the database level.
    /// </remarks>
    public DbSet<ModuleCapabilityGrant> ModuleCapabilityGrants => Set<ModuleCapabilityGrant>();

    // Authentication (OpenIddict) DbSets
    /// <summary>
    /// Gets or sets the OpenIddict Applications DbSet.
    /// </summary>
    /// <remarks>
    /// Represents OAuth2/OIDC client applications registered in the system.
    /// Includes client IDs, secrets, redirect URIs, and permissions.
    /// </remarks>
    public DbSet<OpenIddictApplication> OpenIddictApplications => Set<OpenIddictApplication>();

    /// <summary>
    /// Gets or sets the OpenIddict Authorizations DbSet.
    /// </summary>
    /// <remarks>
    /// Represents user consents/authorizations granted to applications.
    /// Tracks which users have authorized which applications with specific scopes.
    /// </remarks>
    public DbSet<OpenIddictAuthorization> OpenIddictAuthorizations => Set<OpenIddictAuthorization>();

    /// <summary>
    /// Gets or sets the OpenIddict Tokens DbSet.
    /// </summary>
    /// <remarks>
    /// Represents OAuth2/OIDC tokens (access tokens, refresh tokens, ID tokens, authorization codes).
    /// Used for token validation, revocation, and audit trails.
    /// </remarks>
    public DbSet<OpenIddictToken> OpenIddictTokens => Set<OpenIddictToken>();

    /// <summary>
    /// Gets or sets the OpenIddict Scopes DbSet.
    /// </summary>
    /// <remarks>
    /// Represents available OAuth2/OIDC scopes that applications can request.
    /// Includes both standard OIDC scopes (openid, profile, email) and custom scopes.
    /// </remarks>
    public DbSet<OpenIddictScope> OpenIddictScopes => Set<OpenIddictScope>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure specific entity models
        // (Naming strategy is applied in each entity configuration)
        ConfigureIdentityModels(modelBuilder);
        ConfigureOrganizationModels(modelBuilder);
        ConfigurePermissionModels(modelBuilder);
        ConfigureSettingModels(modelBuilder);
        ConfigureDeviceModels(modelBuilder);
        ConfigureModuleModels(modelBuilder);
        ConfigureAuthenticationModels(modelBuilder);
    }

    /// <summary>
    /// Configures identity entities.
    /// Includes ApplicationUser and ApplicationRole.
    /// </summary>
    private void ConfigureIdentityModels(ModelBuilder modelBuilder)
    {
        // Apply configurations for all identity entities
        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationRoleConfiguration());
    }

    /// <summary>
    /// Configures organization and team hierarchy entities.
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
        // Apply configurations for all settings entities
        modelBuilder.ApplyConfiguration(new SystemSettingConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationSettingConfiguration());
        modelBuilder.ApplyConfiguration(new UserSettingConfiguration());
    }

    /// <summary>
    /// Configures device and session entities.
    /// Includes UserDevice for tracking user devices.
    /// </summary>
    private void ConfigureDeviceModels(ModelBuilder modelBuilder)
    {
        // Apply configurations for all device entities
        modelBuilder.ApplyConfiguration(new UserDeviceConfiguration());
    }

    /// <summary>
    /// Configures module registry and capability grant entities.
    /// Includes InstalledModule and ModuleCapabilityGrant.
    /// </summary>
    private void ConfigureModuleModels(ModelBuilder modelBuilder)
    {
        // Apply configurations for all module entities
        modelBuilder.ApplyConfiguration(new InstalledModuleConfiguration());
        modelBuilder.ApplyConfiguration(new ModuleCapabilityGrantConfiguration());
    }

    /// <summary>
    /// Configures authentication entities (OpenIddict).
    /// Includes OpenIddictApplication, OpenIddictAuthorization, OpenIddictToken, and OpenIddictScope.
    /// </summary>
    private void ConfigureAuthenticationModels(ModelBuilder modelBuilder)
    {
        // Apply configurations for all authentication entities
        modelBuilder.ApplyConfiguration(new OpenIddictApplicationConfiguration(_namingStrategy));
        modelBuilder.ApplyConfiguration(new OpenIddictAuthorizationConfiguration(_namingStrategy));
        modelBuilder.ApplyConfiguration(new OpenIddictTokenConfiguration(_namingStrategy));
        modelBuilder.ApplyConfiguration(new OpenIddictScopeConfiguration(_namingStrategy));
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Add TimestampInterceptor for automatic timestamp management
        optionsBuilder.AddInterceptors(new TimestampInterceptor());
    }
}
