using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Initialization;

/// <summary>
/// Seeds the initial admin user on first startup using credentials
/// provided via environment variables or the one-time seed file
/// written by <c>dotnetcloud setup</c>.
/// </summary>
/// <remarks>
/// This seeder is idempotent — it only creates a user when none exist.
/// The admin email is read from configuration (<c>DotNetCloud:AdminEmail</c>).
/// The admin password is read from either:
/// <list type="bullet">
///   <item><c>DotNetCloud:AdminPassword</c> environment variable (set by <c>dotnetcloud start</c>)</item>
///   <item>A one-time seed file (<c>.admin-seed</c>) in the config directory
///         (written by <c>dotnetcloud setup</c>, deleted after reading)</item>
/// </list>
/// Once the admin user is created, credentials are never read again.
/// </remarks>
internal sealed class AdminSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        ILogger<AdminSeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Creates the admin user if no users exist and credentials are available.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = _configuration["DotNetCloud:AdminEmail"];

        // On existing installations, ensure the configured admin account has the Administrator role.
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingAdmin = await _userManager.FindByEmailAsync(email);
            if (existingAdmin is not null)
            {
                await EnsureAdministratorRoleExistsAsync();

                if (!await _userManager.IsInRoleAsync(existingAdmin, "Administrator"))
                {
                    var assignRoleResult = await _userManager.AddToRoleAsync(existingAdmin, "Administrator");
                    if (!assignRoleResult.Succeeded)
                    {
                        var errors = string.Join("; ", assignRoleResult.Errors.Select(e => e.Description));
                        _logger.LogError("Failed to assign Administrator role to existing admin {Email}: {Errors}", email, errors);
                        throw new InvalidOperationException($"Admin role assignment failed: {errors}");
                    }

                    _logger.LogInformation("Assigned Administrator role to existing admin user {Email}.", email);
                }

                return;
            }
        }

        // Only seed when the database has zero users (first run)
        var userCount = _userManager.Users.Count();
        if (userCount > 0)
        {
            _logger.LogDebug("Users already exist ({Count}). Skipping admin seed.", userCount);
            return;
        }

        var password = ReadAdminPassword();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "No admin credentials provided. " +
                "Run 'dotnetcloud setup' to configure the initial admin account.");
            return;
        }

        _logger.LogInformation("Creating initial admin user {Email}...", email);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Administrator",
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create admin user: {Errors}", errors);
            throw new InvalidOperationException($"Admin user creation failed: {errors}");
        }

        await EnsureAdministratorRoleExistsAsync();

        var roleResult = await _userManager.AddToRoleAsync(user, "Administrator");
        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to assign Administrator role: {Errors}", errors);
            throw new InvalidOperationException($"Admin role assignment failed: {errors}");
        }

        _logger.LogInformation("Admin user {Email} created and assigned Administrator role.", email);
    }

    private async Task EnsureAdministratorRoleExistsAsync()
    {
        if (await _roleManager.RoleExistsAsync("Administrator"))
        {
            return;
        }

        var identityRole = new ApplicationRole
        {
            Name = "Administrator",
            Description = "Full system administrator",
            IsSystemRole = true
        };

        var createRoleResult = await _roleManager.CreateAsync(identityRole);
        if (!createRoleResult.Succeeded)
        {
            var errors = string.Join("; ", createRoleResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create Administrator identity role: {Errors}", errors);
            throw new InvalidOperationException($"Identity role creation failed: {errors}");
        }

        _logger.LogInformation("Created Administrator role in identity store.");
    }

    /// <summary>
    /// Reads the admin password from environment variable or one-time seed file.
    /// The seed file is deleted after reading to ensure the password is not persisted.
    /// </summary>
    private string? ReadAdminPassword()
    {
        // 1. Environment variable (set by dotnetcloud start)
        var password = _configuration["DotNetCloud:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(password))
        {
            return password;
        }

        // 2. One-time seed file (written by dotnetcloud setup)
        var configDir = _configuration["DOTNETCLOUD_CONFIG_DIR"]
            ?? (OperatingSystem.IsWindows()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "dotnetcloud")
                : "/etc/dotnetcloud");

        var seedFile = Path.Combine(configDir, ".admin-seed");

        if (!File.Exists(seedFile))
        {
            return null;
        }

        try
        {
            password = File.ReadAllText(seedFile).Trim();
            File.Delete(seedFile);
            _logger.LogInformation("Admin seed file consumed and deleted.");
            return string.IsNullOrWhiteSpace(password) ? null : password;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read admin seed file at {Path}.", seedFile);
            return null;
        }
    }
}
