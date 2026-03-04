using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Permissions;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// Initializes a <see cref="CoreDbContext"/> with seed data for integration tests.
/// </summary>
internal static class DatabaseSeeder
{
    /// <summary>
    /// Creates a fresh <see cref="CoreDbContext"/> backed by an in-memory database.
    /// </summary>
    /// <param name="databaseName">A unique name for the in-memory database instance.</param>
    /// <param name="namingStrategy">Optional naming strategy; defaults to <see cref="PostgreSqlNamingStrategy"/>.</param>
    public static CoreDbContext CreateInMemoryContext(
        string? databaseName = null,
        ITableNamingStrategy? namingStrategy = null)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"IntTest_{Guid.NewGuid():N}")
            .Options;

        return new CoreDbContext(options, namingStrategy ?? new PostgreSqlNamingStrategy());
    }

    /// <summary>
    /// Seeds the database with minimal default data required for integration tests.
    /// </summary>
    public static async Task SeedDefaultDataAsync(CoreDbContext context)
    {
        // Seed default Identity roles (context.Roles is DbSet<Role> from Permissions;
        // Identity roles use Set<ApplicationRole>())
        var identityRoles = context.Set<ApplicationRole>();
        if (!await identityRoles.AnyAsync())
        {
            identityRoles.AddRange(
                new ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    Name = "Administrator",
                    NormalizedName = "ADMINISTRATOR",
                    Description = "Full system access",
                    IsSystemRole = true,
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                },
                new ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    Name = "User",
                    NormalizedName = "USER",
                    Description = "Standard user access",
                    IsSystemRole = true,
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                },
                new ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    Name = "Guest",
                    NormalizedName = "GUEST",
                    Description = "Read-only guest access",
                    IsSystemRole = true,
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                },
                new ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    Name = "Moderator",
                    NormalizedName = "MODERATOR",
                    Description = "Content moderation access",
                    IsSystemRole = true,
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                });
        }

        // Seed core permissions
        if (!await context.Permissions.AnyAsync())
        {
            context.Permissions.AddRange(
                new Permission { Id = Guid.NewGuid(), Code = "core.admin", DisplayName = "System Administration" },
                new Permission { Id = Guid.NewGuid(), Code = "core.users.view", DisplayName = "View Users" },
                new Permission { Id = Guid.NewGuid(), Code = "core.users.manage", DisplayName = "Manage Users" },
                new Permission { Id = Guid.NewGuid(), Code = "core.settings.view", DisplayName = "View Settings" },
                new Permission { Id = Guid.NewGuid(), Code = "core.settings.manage", DisplayName = "Manage Settings" },
                new Permission { Id = Guid.NewGuid(), Code = "core.modules.view", DisplayName = "View Modules" },
                new Permission { Id = Guid.NewGuid(), Code = "core.modules.manage", DisplayName = "Manage Modules" });
        }

        // Seed default system settings
        if (!await context.SystemSettings.AnyAsync())
        {
            context.SystemSettings.AddRange(
                new SystemSetting
                {
                    Module = "core",
                    Key = "instance.name",
                    Value = "DotNetCloud Test Instance",
                    Description = "Instance name for testing",
                    UpdatedAt = DateTime.UtcNow,
                },
                new SystemSetting
                {
                    Module = "core",
                    Key = "instance.url",
                    Value = "https://localhost",
                    Description = "Base URL for testing",
                    UpdatedAt = DateTime.UtcNow,
                });
        }

        // Seed a default organization
        if (!await context.Organizations.AnyAsync())
        {
            context.Organizations.Add(new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Test Organization",
                Description = "Default test organization",
                CreatedAt = DateTime.UtcNow,
            });
        }

        await context.SaveChangesAsync();
    }
}
