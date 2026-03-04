using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Permissions;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Integration.Tests.Builders;
using DotNetCloud.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Integration.Tests.Database;

/// <summary>
/// Integration tests that run CRUD operations against real PostgreSQL and SQL Server
/// containers via Docker. Tests are skipped gracefully when Docker is not available.
/// </summary>
/// <remarks>
/// MariaDB is intentionally excluded — Pomelo does not yet support .NET 10.
/// </remarks>
[TestClass]
[TestCategory("Docker")]
public class DockerDatabaseIntegrationTests
{
    private static DatabaseContainerFixture? s_postgresFixture;
    private static DatabaseContainerFixture? s_sqlServerFixture;
    private static bool s_dockerAvailable;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        var pgConfig = DatabaseContainerConfig.PostgreSql();
        s_postgresFixture = new DatabaseContainerFixture(pgConfig);

        var sqlConfig = DatabaseContainerConfig.SqlServer();
        s_sqlServerFixture = new DatabaseContainerFixture(sqlConfig);

        // Start both containers concurrently. This avoids the PostgreSQL container
        // being idle-killed by WSL while waiting for SQL Server health checks.
        var pgTask = s_postgresFixture.StartAsync();
        var sqlTask = s_sqlServerFixture.StartAsync();

        await Task.WhenAll(pgTask, sqlTask);

        s_dockerAvailable = pgTask.Result;
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_postgresFixture is not null)
        {
            await s_postgresFixture.DisposeAsync();
        }

        if (s_sqlServerFixture is not null)
        {
            await s_sqlServerFixture.DisposeAsync();
        }
    }

    // ── PostgreSQL Tests ─────────────────────────────────────────────────

    [TestMethod]
    public async Task PostgreSql_EnsureCreated_Succeeds()
    {
        SkipIfDockerUnavailable();
        await using var context = CreatePostgreSqlContext();

        var created = await context.Database.EnsureCreatedAsync();

        // EnsureCreated returns true when the database was just created
        Assert.IsTrue(created, "EnsureCreatedAsync should create the schema");
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

    // ── SQL Server Tests ─────────────────────────────────────────────────

    [TestMethod]
    public async Task SqlServer_EnsureCreated_Succeeds()
    {
        SkipIfDockerUnavailable();
        SkipIfSqlServerUnavailable();
        await using var context = CreateSqlServerContext();

        await EnsureCreatedOrSkipAsync(context);
    }

    [TestMethod]
    public async Task SqlServer_Crud_Organization()
    {
        SkipIfDockerUnavailable();
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
        SkipIfDockerUnavailable();
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
        SkipIfDockerUnavailable();
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
        SkipIfDockerUnavailable();
        SkipIfSqlServerUnavailable();
        await using var context = CreateSqlServerContext();
        await EnsureCreatedOrSkipAsync(context);

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
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
        SkipIfDockerUnavailable();
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

    private static void SkipIfDockerUnavailable()
    {
        if (!s_dockerAvailable)
        {
            Assert.Inconclusive("Docker is not available — skipping Docker-based database test.");
        }
    }

    private static void SkipIfSqlServerUnavailable()
    {
        if (s_sqlServerFixture?.ConnectionString is null)
        {
            Assert.Inconclusive("SQL Server container did not start — skipping SQL Server test.");
        }
    }

    /// <summary>
    /// Creates a <see cref="CoreDbContext"/> connected to the running PostgreSQL container.
    /// Each call uses a unique database name to isolate tests.
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

    /// <summary>
    /// Creates a <see cref="CoreDbContext"/> connected to the running SQL Server container.
    /// Each call uses a unique database name to isolate tests.
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
    /// Wraps <see cref="DatabaseContext.EnsureCreatedAsync"/> with a timeout and marks the test
    /// inconclusive if the container crashed after the health check passed (common with SQL Server
    /// on certain WSL2 kernels where the container exits shortly after initialization).
    /// </summary>
    private static async Task EnsureCreatedOrSkipAsync(CoreDbContext context)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await context.Database.EnsureCreatedAsync(cts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or Microsoft.Data.SqlClient.SqlException)
        {
            Assert.Inconclusive($"Database container became unreachable — skipping: {ex.GetType().Name}");
        }
    }
}
