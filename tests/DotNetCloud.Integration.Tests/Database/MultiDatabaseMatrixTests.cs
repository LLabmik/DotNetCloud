using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Permissions;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Integration.Tests.Builders;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Integration.Tests.Database;

/// <summary>
/// Integration tests that verify consistent behavior across all supported database providers
/// using in-memory databases with different naming strategies.
/// </summary>
[TestClass]
public class MultiDatabaseMatrixTests
{
    private static ITableNamingStrategy[] AllStrategies =>
    [
        new PostgreSqlNamingStrategy(),
        new SqlServerNamingStrategy(),
        new MariaDbNamingStrategy(),
    ];

    [TestMethod]
    [DataRow("PostgreSQL")]
    [DataRow("SqlServer")]
    [DataRow("MariaDB")]
    public void Context_CreatesSuccessfully_ForEachProvider(string providerName)
    {
        // Arrange
        var strategy = GetStrategy(providerName);
        using var context = CreateContext(strategy);

        // Assert
        Assert.IsNotNull(context);
        Assert.IsNotNull(context.Model);
    }

    [TestMethod]
    public void Schema_EntityTypeCount_IsConsistentAcrossProviders()
    {
        // Arrange
        var counts = new List<int>();

        foreach (var strategy in AllStrategies)
        {
            using var context = CreateContext(strategy);
            counts.Add(context.Model.GetEntityTypes().Count());
        }

        // Assert — all providers configure the same number of entities
        Assert.IsTrue(counts.Distinct().Count() == 1,
            $"Entity type counts differ across providers: {string.Join(", ", counts)}");
    }

    [TestMethod]
    public void Schema_EntityNames_AreConsistentAcrossProviders()
    {
        // Arrange
        var entitySets = new List<HashSet<string>>();

        foreach (var strategy in AllStrategies)
        {
            using var context = CreateContext(strategy);
            var names = context.Model.GetEntityTypes()
                .Select(e => e.ClrType.FullName!)
                .ToHashSet();
            entitySets.Add(names);
        }

        // Assert — all providers have the same CLR entity types
        var reference = entitySets[0];
        for (var i = 1; i < entitySets.Count; i++)
        {
            Assert.IsTrue(reference.SetEquals(entitySets[i]),
                $"Entity set {i} differs from reference set");
        }
    }

    [TestMethod]
    [DataRow("PostgreSQL")]
    [DataRow("SqlServer")]
    [DataRow("MariaDB")]
    public async Task Crud_Organization_WorksForEachProvider(string providerName)
    {
        // Arrange
        var strategy = GetStrategy(providerName);
        using var context = CreateContext(strategy);
        var org = new OrganizationBuilder().WithName($"Org-{providerName}").Build();

        // Act — Create
        context.Organizations.Add(org);
        await context.SaveChangesAsync();

        // Assert — Read
        var fetched = await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == org.Id);
        Assert.IsNotNull(fetched, "Organization should be retrievable");
        Assert.AreEqual(org.Name, fetched.Name);

        // Act — Update
        fetched.Description = "Updated";
        context.Organizations.Update(fetched);
        await context.SaveChangesAsync();

        var updated = await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == org.Id);
        Assert.AreEqual("Updated", updated!.Description);

        // Act — Soft delete
        updated.IsDeleted = true;
        updated.DeletedAt = DateTime.UtcNow;
        context.Organizations.Update(updated);
        await context.SaveChangesAsync();

        // Without IgnoreQueryFilters, soft-deleted entities should be hidden
        var filtered = await context.Organizations.FirstOrDefaultAsync(o => o.Id == org.Id);
        Assert.IsNull(filtered, "Soft-deleted organization should be filtered out");
    }

    [TestMethod]
    [DataRow("PostgreSQL")]
    [DataRow("SqlServer")]
    [DataRow("MariaDB")]
    public async Task Crud_User_WorksForEachProvider(string providerName)
    {
        // Arrange
        var strategy = GetStrategy(providerName);
        using var context = CreateContext(strategy);
        var user = new ApplicationUserBuilder()
            .WithEmail($"user-{providerName}@test.local")
            .WithDisplayName($"User {providerName}")
            .Build();

        // Act — Create
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert — Read
        var fetched = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual(user.Email, fetched.Email);
        Assert.AreEqual(user.DisplayName, fetched.DisplayName);
        Assert.IsTrue(fetched.IsActive);
    }

    [TestMethod]
    [DataRow("PostgreSQL")]
    [DataRow("SqlServer")]
    [DataRow("MariaDB")]
    public async Task Crud_SystemSetting_WorksForEachProvider(string providerName)
    {
        // Arrange
        var strategy = GetStrategy(providerName);
        using var context = CreateContext(strategy);
        var setting = new SystemSetting
        {
            Module = "core",
            Key = $"test.key.{providerName}",
            Value = "test-value",
            Description = "Integration test setting",
            UpdatedAt = DateTime.UtcNow,
        };

        // Act — Create
        context.SystemSettings.Add(setting);
        await context.SaveChangesAsync();

        // Assert — Read
        var fetched = await context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "core" && s.Key == setting.Key);
        Assert.IsNotNull(fetched);
        Assert.AreEqual("test-value", fetched.Value);
    }

    [TestMethod]
    [DataRow("PostgreSQL")]
    [DataRow("SqlServer")]
    [DataRow("MariaDB")]
    public async Task Crud_Permission_WorksForEachProvider(string providerName)
    {
        // Arrange
        var strategy = GetStrategy(providerName);
        using var context = CreateContext(strategy);
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Code = $"test.{providerName}.read",
            DisplayName = $"Test {providerName} Read",
            Description = "Integration test permission",
        };

        // Act
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        // Assert
        var fetched = await context.Permissions.FirstOrDefaultAsync(p => p.Id == permission.Id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual(permission.Code, fetched.Code);
    }

    [TestMethod]
    public void ProviderDetection_PostgreSQL_IsDetected()
    {
        var provider = DatabaseProviderDetector.DetectProvider(
            "Host=localhost;Port=5432;Database=testdb;Username=postgres;Password=pass");
        Assert.AreEqual(DatabaseProvider.PostgreSQL, provider);
    }

    [TestMethod]
    public void ProviderDetection_SqlServer_IsDetected()
    {
        var provider = DatabaseProviderDetector.DetectProvider(
            "Data Source=localhost;Initial Catalog=testdb;Integrated Security=true");
        Assert.AreEqual(DatabaseProvider.SqlServer, provider);
    }

    [TestMethod]
    public void ProviderDetection_MariaDB_IsDetected()
    {
        var provider = DatabaseProviderDetector.DetectProvider(
            "Server=localhost;Port=3306;Database=testdb;User=root;Password=pass");
        Assert.AreEqual(DatabaseProvider.MariaDB, provider);
    }

    [TestMethod]
    public void NamingStrategy_GetNamingStrategy_ReturnsCorrectType()
    {
        Assert.IsInstanceOfType<PostgreSqlNamingStrategy>(
            DatabaseProviderDetector.GetNamingStrategy(DatabaseProvider.PostgreSQL));
        Assert.IsInstanceOfType<SqlServerNamingStrategy>(
            DatabaseProviderDetector.GetNamingStrategy(DatabaseProvider.SqlServer));
        Assert.IsInstanceOfType<MariaDbNamingStrategy>(
            DatabaseProviderDetector.GetNamingStrategy(DatabaseProvider.MariaDB));
    }

    private static CoreDbContext CreateContext(ITableNamingStrategy strategy)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"MultiDbTest_{Guid.NewGuid():N}")
            .Options;
        return new CoreDbContext(options, strategy);
    }

    private static ITableNamingStrategy GetStrategy(string providerName) => providerName switch
    {
        "PostgreSQL" => new PostgreSqlNamingStrategy(),
        "SqlServer" => new SqlServerNamingStrategy(),
        "MariaDB" => new MariaDbNamingStrategy(),
        _ => throw new ArgumentException($"Unknown provider: {providerName}"),
    };
}
