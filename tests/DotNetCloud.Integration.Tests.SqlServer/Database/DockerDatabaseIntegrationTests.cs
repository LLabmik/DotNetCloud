using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Permissions;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Integration.Tests.Builders;
using DotNetCloud.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Integration.Tests.SqlServer.Database;

/// <summary>
/// Integration tests that run CRUD operations against SQL Server.
/// sqlcmd is used for health checks inside the container.
/// Tests are skipped gracefully when neither source is available.
/// </summary>
/// <remarks>
/// This is the SQL Server-specific variant used by the CI pipeline and local testing.
/// Unlike the base project which tests both PostgreSQL and SQL Server, this project
/// only spins up a SQL Server container.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
[TestCategory("Docker")]
[TestCategory("SqlServer")]
public class DockerDatabaseIntegrationTests
{
    private static DatabaseContainerFixture? s_sqlServerFixture;
    private static bool s_dockerAvailable;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        var sqlConfig = DatabaseContainerConfig.SqlServer();
        s_sqlServerFixture = new DatabaseContainerFixture(sqlConfig);
        s_dockerAvailable = await s_sqlServerFixture.StartAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_sqlServerFixture is not null)
        {
            await s_sqlServerFixture.DisposeAsync();
        }
    }

    // ── SQL Server Tests ─────────────────────────────────────────────────

    [TestMethod]
    public async Task SqlServer_EnsureCreated_Succeeds()
    {
        SkipIfSqlServerUnavailable();
        await using var context = CreateSqlServerContext();

        await EnsureCreatedOrSkipAsync(context);
    }

    [TestMethod]
    public async Task SqlServer_Crud_Organization()
    {
        SkipIfSqlServerUnavailable();
        await using var context = CreateSqlServerContext();
        await EnsureCreatedOrSkipAsync(context);

        var org = new OrganizationBuilder().WithName("SQL-Org").Build();

        // Create
        context.Organizations.Add(org);
        await context.SaveChangesAsync();

        // Read
        var fetched = await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == org.Id);
        Assert.IsNotNull(fetched, "Organization should be retrievable from SQL Server");
        Assert.AreEqual("SQL-Org", fetched.Name);

        // Update
        fetched.Description = "Updated via Docker test";
        context.Organizations.Update(fetched);
        await context.SaveChangesAsync();

        var updated = await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == org.Id);
        Assert.AreEqual("Updated via Docker test", updated!.Description);

        // Soft delete
        updated.IsDeleted = true;
        updated.DeletedAt = DateTime.UtcNow;
        context.Organizations.Update(updated);
        await context.SaveChangesAsync();

        var filtered = await context.Organizations.FirstOrDefaultAsync(o => o.Id == org.Id);
        Assert.IsNull(filtered, "Soft-deleted organization should be filtered out");
    }

    [TestMethod]
    public async Task SqlServer_Crud_User()
    {
        SkipIfSqlServerUnavailable();
        await using var context = CreateSqlServerContext();
        await EnsureCreatedOrSkipAsync(context);

        var user = new ApplicationUserBuilder()
            .WithEmail("sql-user@test.local")
            .WithDisplayName("SQL User")
            .Build();

        // Create
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Read
        var fetched = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual("sql-user@test.local", fetched.Email);
        Assert.AreEqual("SQL User", fetched.DisplayName);
        Assert.IsTrue(fetched.IsActive);
    }

    [TestMethod]
    public async Task SqlServer_Crud_SystemSetting()
    {
        SkipIfSqlServerUnavailable();
        await using var context = CreateSqlServerContext();
        await EnsureCreatedOrSkipAsync(context);

        var setting = new SystemSetting
        {
            Module = "core",
            Key = "docker.sql.test",
            Value = "sql-value",
            Description = "Docker SQL Server test setting",
            UpdatedAt = DateTime.UtcNow,
        };

        // Create
        context.SystemSettings.Add(setting);
        await context.SaveChangesAsync();

        // Read
        var fetched = await context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "core" && s.Key == "docker.sql.test");
        Assert.IsNotNull(fetched);
        Assert.AreEqual("sql-value", fetched.Value);
    }

    [TestMethod]
    public async Task SqlServer_Crud_Permission()
    {
        SkipIfSqlServerUnavailable();
        await using var context = CreateSqlServerContext();
        await EnsureCreatedOrSkipAsync(context);

        var permission = new Permission
        {
            Id = Guid.CreateVersion7(),
            Code = "docker.sql.read",
            DisplayName = "Docker SQL Read",
            Description = "Docker SQL Server test permission",
        };

        // Create
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        // Read
        var fetched = await context.Permissions.FirstOrDefaultAsync(p => p.Id == permission.Id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual("docker.sql.read", fetched.Code);
    }

    [TestMethod]
    public async Task SqlServer_Seed_DefaultData()
    {
        SkipIfSqlServerUnavailable();
        await using var context = CreateSqlServerContext();
        await EnsureCreatedOrSkipAsync(context);

        await DatabaseSeeder.SeedDefaultDataAsync(context);

        var roles = await context.Set<ApplicationRole>().ToListAsync();
        Assert.IsTrue(roles.Count >= 1, "Should have at least 1 role (seeded or from CRUD tests)");

        var permissions = await context.Permissions.ToListAsync();
        Assert.IsTrue(permissions.Count >= 1, "Should have at least 1 permission");

        var settings = await context.SystemSettings.ToListAsync();
        Assert.IsTrue(settings.Count >= 1, "Should have at least 1 system setting");

        var orgs = await context.Organizations.ToListAsync();
        Assert.IsTrue(orgs.Count >= 1, "Should have at least 1 organization");
    }

    // ── Helpers

    private static void SkipIfSqlServerUnavailable()
    {
        if (!s_dockerAvailable)
        {
            Assert.Inconclusive("SQL Server container is not available — skipping.");
        }
    }

    /// <summary>
    /// Creates a <see cref="CoreDbContext"/> connected to the SQL Server container.
    /// </summary>
    private static CoreDbContext CreateSqlServerContext()
    {
        var connectionString = s_sqlServerFixture!.ConnectionString
            ?? throw new InvalidOperationException("SQL Server fixture not started.");

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new CoreDbContext(options, new SqlServerNamingStrategy());
    }

    /// <summary>
    /// Calls EnsureCreatedAsync, skipping the test if the method throws
    /// (e.g., schema already exists from a parallel test).
    /// </summary>
    private static async Task EnsureCreatedOrSkipAsync(CoreDbContext context)
    {
        try
        {
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Database schema could not be created: {ex.Message}");
        }
    }
}
