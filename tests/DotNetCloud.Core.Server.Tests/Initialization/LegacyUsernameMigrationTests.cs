using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Server.Initialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Initialization;

[TestClass]
public class LegacyUsernameMigrationTests
{
    private static CoreDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new CoreDbContext(options, new PostgreSqlNamingStrategy());
    }

    private static ApplicationUser CreateUser(string username, string email, string displayName = "Test User")
    {
        return new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = displayName,
            EmailConfirmed = true,
            IsActive = true,
        };
    }

    [TestMethod]
    public async Task MigrateAsync_UsersWithEmailUsername_RewritesToLocalPart()
    {
        // Arrange
        var dbName = Guid.CreateVersion7().ToString();
        await using (var db = CreateContext(dbName))
        {
            db.Users.AddRange(
                CreateUser("admin@example.com", "admin@example.com"),
                CreateUser("bill.jones@example.com", "bill.jones@example.com"));
            await db.SaveChangesAsync();
        }

        // Act
        var migration = new LegacyUsernameMigration(CreateContext(dbName), NullLogger<LegacyUsernameMigration>.Instance);
        await migration.MigrateAsync();

        // Assert
        await using (var verify = CreateContext(dbName))
        {
            var users = await verify.Users.AsNoTracking().ToListAsync();
            Assert.AreEqual(2, users.Count);

            var admin = users.Single(u => u.Email == "admin@example.com");
            Assert.AreEqual("admin", admin.UserName);
            Assert.AreEqual("ADMIN", admin.NormalizedUserName);

            var bill = users.Single(u => u.Email == "bill.jones@example.com");
            Assert.AreEqual("bill.jones", bill.UserName);
            Assert.AreEqual("BILL.JONES", bill.NormalizedUserName);
        }
    }

    [TestMethod]
    public async Task MigrateAsync_DuplicateLocalParts_AppendsSuffix()
    {
        // Arrange — two users share the local part "admin"
        var dbName = Guid.CreateVersion7().ToString();
        await using (var db = CreateContext(dbName))
        {
            db.Users.AddRange(
                CreateUser("admin@example.com", "admin@example.com"),
                CreateUser("admin@contoso.com", "admin@contoso.com"));
            await db.SaveChangesAsync();
        }

        // Act
        var migration = new LegacyUsernameMigration(CreateContext(dbName), NullLogger<LegacyUsernameMigration>.Instance);
        await migration.MigrateAsync();

        // Assert
        await using (var verify = CreateContext(dbName))
        {
            var usernames = await verify.Users.AsNoTracking().Select(u => u.UserName).ToListAsync();
            Assert.AreEqual(2, usernames.Count);
            Assert.IsTrue(usernames.Contains("admin"), "First local part should keep the base name");
            Assert.IsTrue(usernames.Contains("admin2"), "Collision should be resolved with a numeric suffix");
        }
    }

    [TestMethod]
    public async Task MigrateAsync_NoLegacyUsers_IsNoOp()
    {
        // Arrange — all usernames are already distinct (no '@')
        var dbName = Guid.CreateVersion7().ToString();
        await using (var db = CreateContext(dbName))
        {
            db.Users.AddRange(
                CreateUser("admin", "admin@example.com"),
                CreateUser("bill.jones", "bill.jones@example.com"));
            await db.SaveChangesAsync();
        }

        // Act — run twice (idempotency check)
        var migration = new LegacyUsernameMigration(CreateContext(dbName), NullLogger<LegacyUsernameMigration>.Instance);
        await migration.MigrateAsync();
        await migration.MigrateAsync();

        // Assert
        await using (var verify = CreateContext(dbName))
        {
            var users = await verify.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
            Assert.AreEqual(2, users.Count);
            Assert.AreEqual("admin", users[0].UserName);
            Assert.AreEqual("bill.jones", users[1].UserName);
        }
    }

    [TestMethod]
    public async Task MigrateAsync_PreservesEmail()
    {
        // Arrange
        var dbName = Guid.CreateVersion7().ToString();
        await using (var db = CreateContext(dbName))
        {
            db.Users.AddRange(
                CreateUser("admin@example.com", "admin@example.com"));
            await db.SaveChangesAsync();
        }

        // Act
        var migration = new LegacyUsernameMigration(CreateContext(dbName), NullLogger<LegacyUsernameMigration>.Instance);
        await migration.MigrateAsync();

        // Assert — username migrated, email untouched
        await using (var verify = CreateContext(dbName))
        {
            var user = await verify.Users.AsNoTracking().SingleAsync();
            Assert.AreEqual("admin", user.UserName);
            Assert.AreEqual("admin@example.com", user.Email);
            Assert.AreEqual("ADMIN@EXAMPLE.COM", user.NormalizedEmail);
        }
    }

    [TestMethod]
    public async Task MigrateAsync_CollidesWithExistingUsername_AppendsSuffix()
    {
        // Arrange — a legacy "admin@example.com" and an existing distinct "admin"
        var dbName = Guid.CreateVersion7().ToString();
        await using (var db = CreateContext(dbName))
        {
            db.Users.AddRange(
                CreateUser("admin", "other@example.com"),
                CreateUser("admin@example.com", "admin@example.com"));
            await db.SaveChangesAsync();
        }

        // Act
        var migration = new LegacyUsernameMigration(CreateContext(dbName), NullLogger<LegacyUsernameMigration>.Instance);
        await migration.MigrateAsync();

        // Assert
        await using (var verify = CreateContext(dbName))
        {
            var usernames = await verify.Users.AsNoTracking().Select(u => u.UserName).ToListAsync();
            Assert.IsTrue(usernames.Contains("admin"));
            Assert.IsTrue(usernames.Contains("admin2"), "Should not overwrite the existing 'admin' username");
        }
    }
}
