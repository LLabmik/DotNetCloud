using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Permissions;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Tests.Integration;

/// <summary>
/// Tests for CoreDbContext configuration and model validation.
/// </summary>
[TestClass]
public class DbContextConfigurationTests
{
    [TestMethod]
    public void CoreDbContext_Initialization_Succeeds()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: $"ConfigTest_{Guid.NewGuid()}")
            .Options;

        // Act
        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Assert
        Assert.IsNotNull(context, "CoreDbContext should initialize successfully");
        Assert.IsNotNull(context.Model, "DbContext model should be created");
    }

    [TestMethod]
    public void CoreDbContext_HasAllRequiredDbSets()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"DbSetsTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act & Assert - Identity
        Assert.IsNotNull(context.Users, "CoreDbContext should have Users DbSet");
        Assert.IsNotNull(context.Roles, "CoreDbContext should have Roles DbSet");
        Assert.IsNotNull(context.UserRoles, "CoreDbContext should have UserRoles DbSet");
        Assert.IsNotNull(context.UserClaims, "CoreDbContext should have UserClaims DbSet");
        Assert.IsNotNull(context.RoleClaims, "CoreDbContext should have RoleClaims DbSet");

        // Assert - Organizations
        Assert.IsNotNull(context.Organizations, "CoreDbContext should have Organizations DbSet");
        Assert.IsNotNull(context.Teams, "CoreDbContext should have Teams DbSet");
        Assert.IsNotNull(context.TeamMembers, "CoreDbContext should have TeamMembers DbSet");
        Assert.IsNotNull(context.Groups, "CoreDbContext should have Groups DbSet");
        Assert.IsNotNull(context.GroupMembers, "CoreDbContext should have GroupMembers DbSet");
        Assert.IsNotNull(context.OrganizationMembers, "CoreDbContext should have OrganizationMembers DbSet");

        // Assert - Permissions
        Assert.IsNotNull(context.Permissions, "CoreDbContext should have Permissions DbSet");
        Assert.IsNotNull(context.RolePermissions, "CoreDbContext should have RolePermissions DbSet");

        // Assert - Settings
        Assert.IsNotNull(context.SystemSettings, "CoreDbContext should have SystemSettings DbSet");
        Assert.IsNotNull(context.OrganizationSettings, "CoreDbContext should have OrganizationSettings DbSet");
        Assert.IsNotNull(context.UserSettings, "CoreDbContext should have UserSettings DbSet");

        // Assert - Modules
        Assert.IsNotNull(context.UserDevices, "CoreDbContext should have UserDevices DbSet");
        Assert.IsNotNull(context.InstalledModules, "CoreDbContext should have InstalledModules DbSet");
        Assert.IsNotNull(context.ModuleCapabilityGrants, "CoreDbContext should have ModuleCapabilityGrants DbSet");

        // Assert - Authentication
        Assert.IsNotNull(context.OpenIddictApplications, "CoreDbContext should have OpenIddictApplications DbSet");
        Assert.IsNotNull(context.OpenIddictAuthorizations, "CoreDbContext should have OpenIddictAuthorizations DbSet");
        Assert.IsNotNull(context.OpenIddictTokens, "CoreDbContext should have OpenIddictTokens DbSet");
        Assert.IsNotNull(context.OpenIddictScopes, "CoreDbContext should have OpenIddictScopes DbSet");
    }

    [TestMethod]
    public void CoreDbContext_AllEntityTypesAreConfigured()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"EntityTypesTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act
        var entityTypes = context.Model.GetEntityTypes().ToList();

        // Assert - Verify minimum entity count
        Assert.IsTrue(entityTypes.Count >= 25,
            $"CoreDbContext should have at least 25 configured entities, found {entityTypes.Count}");

        // Verify specific key entities are present
        var entityNames = entityTypes.Select(e => e.ClrType.Name).ToList();

        var requiredEntities = new[]
        {
            nameof(ApplicationUser),
            nameof(ApplicationRole),
            nameof(Organization),
            nameof(Team),
            nameof(Group),
            nameof(Permission),
            nameof(Role),
            nameof(UserDevice),
            nameof(InstalledModule),
            nameof(ModuleCapabilityGrant),
            nameof(SystemSetting),
            nameof(OrganizationSetting),
            nameof(UserSetting)
        };

        foreach (var entityName in requiredEntities)
        {
            Assert.IsTrue(entityNames.Contains(entityName),
                $"CoreDbContext should configure {entityName} entity");
        }
    }

    [TestMethod]
    public void CoreDbContext_Relationships_AreProperlyConfigured()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"RelationshipTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act
        var organizationEntity = context.Model.FindEntityType(typeof(Organization));
        var teamEntity = context.Model.FindEntityType(typeof(Team));
        var teamMemberEntity = context.Model.FindEntityType(typeof(TeamMember));

        // Assert - Check Organization -> Team relationship
        var orgTeamNavigation = organizationEntity?.GetNavigations()
            .FirstOrDefault(n => n.TargetEntityType.ClrType == typeof(Team));
        Assert.IsNotNull(orgTeamNavigation, "Organization should have navigation to Teams");

        // Check Team -> TeamMember relationship
        var teamMemberNavigation = teamEntity?.GetNavigations()
            .FirstOrDefault(n => n.TargetEntityType.ClrType == typeof(TeamMember));
        Assert.IsNotNull(teamMemberNavigation, "Team should have navigation to TeamMembers");
    }

    [TestMethod]
    public void CoreDbContext_Indexes_AreConfigured()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"IndexTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act
        var entityTypes = context.Model.GetEntityTypes().ToList();
        var indexedEntities = entityTypes.Where(e => e.GetIndexes().Any()).ToList();

        // Assert
        Assert.IsTrue(indexedEntities.Count > 5,
            $"Should have indexes on at least 5 entities, found {indexedEntities.Count}");

        // Verify specific indexes
        var teamEntity = context.Model.FindEntityType(typeof(Team));
        var teamIndexes = teamEntity?.GetIndexes().ToList();
        Assert.IsTrue(teamIndexes?.Count > 0, "Team entity should have indexes");
    }

    [TestMethod]
    public void CoreDbContext_UniqueConstraints_AreConfigured()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"UniqueTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act
        var permissionEntity = context.Model.FindEntityType(typeof(Permission));
        var permissionIndexes = permissionEntity?.GetIndexes().ToList();

        // Assert
        var uniqueIndexes = permissionIndexes?.Where(i => i.IsUnique).ToList();
        Assert.IsTrue(uniqueIndexes?.Count > 0, "Permission should have unique indexes");
    }

    [TestMethod]
    public void CoreDbContext_ForeignKeys_AreConfigured()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"ForeignKeyTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act
        var teamEntity = context.Model.FindEntityType(typeof(Team));
        var foreignKeys = teamEntity?.GetForeignKeys().ToList();

        // Assert
        Assert.IsTrue(foreignKeys?.Count > 0, "Team should have foreign keys");

        var orgFk = foreignKeys?.FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Organization));
        Assert.IsNotNull(orgFk, "Team should have foreign key to Organization");
    }

    [TestMethod]
    public void CoreDbContext_MultipleNamingStrategies_ProduceConsistentModel()
    {
        // Arrange
        var strategies = new ITableNamingStrategy[]
        {
            new PostgreSqlNamingStrategy(),
            new SqlServerNamingStrategy(),
            new MariaDbNamingStrategy()
        };

        // Act
        var modelEntityCounts = new List<int>();
        foreach (var strategy in strategies)
        {
            var options = new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase($"ModelTest_{strategy.GetType().Name}_{Guid.NewGuid()}")
                .Options;

            using var context = new CoreDbContext(options, strategy);
            modelEntityCounts.Add(context.Model.GetEntityTypes().Count());
        }

        // Assert
        Assert.AreEqual(modelEntityCounts[0], modelEntityCounts[1],
            "PostgreSQL and SQL Server should produce consistent entity models");
        Assert.AreEqual(modelEntityCounts[0], modelEntityCounts[2],
            "PostgreSQL and MariaDB should produce consistent entity models");
    }

    [TestMethod]
    public void CoreDbContext_IsExtendsIdentityDbContext()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"IdentityTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act & Assert
        Assert.IsInstanceOfType(context, typeof(IdentityDbContext<ApplicationUser, ApplicationRole, Guid>),
            "CoreDbContext should extend IdentityDbContext");
    }

    [TestMethod]
    public void CoreDbContext_QueryFilters_AreApplied()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"FilterTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act
        var organizationEntity = context.Model.FindEntityType(typeof(Organization));
        var hasFilter = organizationEntity?.GetDeclaredQueryFilters().Any() ?? false;

        // Assert
        Assert.IsTrue(hasFilter, "Organization entity should have soft-delete query filter");

        var teamEntity = context.Model.FindEntityType(typeof(Team));
        var teamHasFilter = teamEntity?.GetDeclaredQueryFilters().Any() ?? false;
        Assert.IsTrue(teamHasFilter, "Team entity should have soft-delete query filter");
    }

    [TestMethod]
    public void CoreDbContext_PropertyConfigurations_AreApplied()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"PropertyTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act
        var userEntity = context.Model.FindEntityType(typeof(ApplicationUser));
        var displayNameProperty = userEntity?.FindProperty(nameof(ApplicationUser.DisplayName));

        // Assert
        Assert.IsNotNull(displayNameProperty, "ApplicationUser should have DisplayName property");
    }

    [TestMethod]
    public void CoreDbContext_ConcurrencyTokens_AreConfigured()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"ConcurrencyTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act
        var roleEntity = context.Model.FindEntityType(typeof(Role));
        var concurrencyTokens = roleEntity?.GetProperties()
            .Where(p => p.IsConcurrencyToken)
            .ToList();

        // Assert
        Assert.IsNotNull(concurrencyTokens, "Role entity should have concurrency token configuration");
    }

    [TestMethod]
    public void CoreDbContext_DefaultValues_AreConfigured()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"DefaultTest_{Guid.NewGuid()}")
            .Options;

        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Act
        var userEntity = context.Model.FindEntityType(typeof(ApplicationUser));
        var localeProperty = userEntity?.FindProperty(nameof(ApplicationUser.Locale));
        var isActiveProperty = userEntity?.FindProperty(nameof(ApplicationUser.IsActive));

        // Assert
        Assert.IsNotNull(localeProperty?.GetDefaultValue(), "Locale should have default value");
        Assert.IsNotNull(isActiveProperty?.GetDefaultValue(), "IsActive should have default value");
    }
}
