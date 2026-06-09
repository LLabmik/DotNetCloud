using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Permissions;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Integration.Tests.Builders;
using DotNetCloud.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Integration.Tests.PostgreSQL.Database;

/// <summary>
/// Integration tests that run CRUD operations against a real PostgreSQL database
/// via Docker container. Tests are skipped gracefully when Docker is unavailable.
/// </summary>
/// <remarks>
/// This is the PostgreSQL-specific variant used by the CI pipeline and local testing.
/// Unlike the base project which tests both PostgreSQL and SQL Server, this project
/// only spins up a PostgreSQL container.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
[TestCategory("Docker")]
[TestCategory("PostgreSQL")]
public class DockerDatabaseIntegrationTests
{
    private static DatabaseContainerFixture? s_postgresFixture;
    private static bool s_dockerAvailable;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        var pgConfig = DatabaseContainerConfig.PostgreSql();
        s_postgresFixture = new DatabaseContainerFixture(pgConfig);
        s_dockerAvailable = await s_postgresFixture.StartAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_postgresFixture is not null)
        {
            await s_postgresFixture.DisposeAsync();
        }
    }

    // ── PostgreSQL Tests ─────────────────────────────────────────────────

    [TestMethod]
    public async Task PostgreSql_EnsureCreated_Succeeds()
    {
        SkipIfDockerUnavailable();
        await using var context = CreatePostgreSqlContext();

        // EnsureCreated returns true when schema is freshly created, false when
        // it already exists. Other tests in this class may run first and create
        // the schema, so we only verify the call completes without error.
        await context.Database.EnsureCreatedAsync();
    }

    [TestMethod]
    public async Task PostgreSql_Crud_Organization()
    {
        SkipIfDockerUnavailable();
        await using var context = CreatePostgreSqlContext();
        await context.Database.EnsureCreatedAsync();

        var org = new OrganizationBuilder().WithName("PG-Org").Build();

        // Create
        context.Organizations.Add(org);
        await context.SaveChangesAsync();

        // Read
        var fetched = await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == org.Id);
        Assert.IsNotNull(fetched, "Organization should be retrievable from PostgreSQL");
        Assert.AreEqual("PG-Org", fetched.Name);

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
    public async Task PostgreSql_Crud_User()
    {
        SkipIfDockerUnavailable();
        await using var context = CreatePostgreSqlContext();
        await context.Database.EnsureCreatedAsync();

        var user = new ApplicationUserBuilder()
            .WithEmail("pg-user@test.local")
            .WithDisplayName("PG User")
            .Build();

        // Create
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Read
        var fetched = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual("pg-user@test.local", fetched.Email);
        Assert.AreEqual("PG User", fetched.DisplayName);
        Assert.IsTrue(fetched.IsActive);
    }

    [TestMethod]
    public async Task PostgreSql_Crud_SystemSetting()
    {
        SkipIfDockerUnavailable();
        await using var context = CreatePostgreSqlContext();
        await context.Database.EnsureCreatedAsync();

        var setting = new SystemSetting
        {
            Module = "core",
            Key = "docker.pg.test",
            Value = "pg-value",
            Description = "Docker PostgreSQL test setting",
            UpdatedAt = DateTime.UtcNow,
        };

        // Create
        context.SystemSettings.Add(setting);
        await context.SaveChangesAsync();

        // Read
        var fetched = await context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "core" && s.Key == "docker.pg.test");
        Assert.IsNotNull(fetched);
        Assert.AreEqual("pg-value", fetched.Value);
    }

    [TestMethod]
    public async Task PostgreSql_Crud_Permission()
    {
        SkipIfDockerUnavailable();
        await using var context = CreatePostgreSqlContext();
        await context.Database.EnsureCreatedAsync();

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Code = "docker.pg.read",
            DisplayName = "Docker PG Read",
            Description = "Docker PostgreSQL test permission",
        };

        // Create
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        // Read
        var fetched = await context.Permissions.FirstOrDefaultAsync(p => p.Id == permission.Id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual("docker.pg.read", fetched.Code);
    }

    [TestMethod]
    public async Task PostgreSql_Seed_DefaultData()
    {
        SkipIfDockerUnavailable();
        await using var context = CreatePostgreSqlContext();
        await context.Database.EnsureCreatedAsync();

        await DatabaseSeeder.SeedDefaultDataAsync(context);

        // Seed is idempotent — if CRUD tests ran first, some data already exists
        // and the seeder skips those tables. The key validation is that seeding
        // completes without error against a real PostgreSQL database.
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

    private static void SkipIfDockerUnavailable()
    {
        if (!s_dockerAvailable)
        {
            Assert.Inconclusive("Docker is not available — skipping Docker-based database test.");
        }
    }

    /// <summary>
    /// Creates a <see cref="CoreDbContext"/> connected to the running PostgreSQL container.
    /// </summary>
    private static CoreDbContext CreatePostgreSqlContext()
    {
        var connectionString = s_postgresFixture!.ConnectionString
            ?? throw new InvalidOperationException("PostgreSQL fixture not started.");

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new CoreDbContext(options, new PostgreSqlNamingStrategy());
    }
}
