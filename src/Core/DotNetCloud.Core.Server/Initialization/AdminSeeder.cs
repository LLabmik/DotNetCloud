using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    private readonly CoreDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        CoreDbContext dbContext,
        IConfiguration configuration,
        ILogger<AdminSeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Creates the admin user if no users exist and credentials are available.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = GetConfigValue("DotNetCloud:AdminEmail", "adminEmail");

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
                        _logger.LogError("Failed to assign Administrator role to existing admin: {Errors}", errors);
                        throw new InvalidOperationException($"Admin role assignment failed: {errors}");
                    }

                    _logger.LogInformation("Assigned Administrator role to existing admin user.");
                }

                // Apply MFA enrollment flag for existing admin users when MFA was
                // enabled during setup but the user hasn't completed enrollment yet.
                // This handles the case where the admin user was created by an older
                // version before MfaSetupRequired existed, or where setup was re-run
                // with MFA enabled for an existing installation.
                await ApplyMfaSetupFlagAsync(existingAdmin);

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

        _logger.LogInformation("Creating initial admin user...");

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

        // Add admin to all organizations as a member, which implicitly adds them
        // to the built-in All Users group.
        await AddAdminToAllOrganizationsAsync(user);

        // If MFA was requested during CLI setup, flag the user for MFA enrollment.
        // The web UI redirects to /auth/mfa-setup on first login.
        var adminMfaEnabled = GetConfigValue("DotNetCloud:AdminMfaEnabled", "enableAdminMfa");
        if (string.Equals(adminMfaEnabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            user.MfaSetupRequired = true;
            var updateResult = await _userManager.UpdateAsync(user);
            if (updateResult.Succeeded)
            {
                _logger.LogInformation("MFA setup flagged for admin user {UserId}.", user.Id);
            }
            else
            {
                var updateErrors = string.Join("; ", updateResult.Errors.Select(e => e.Description));
                _logger.LogWarning(
                    "Could not flag MFA setup for admin user {UserId}: {Errors}",
                    user.Id, updateErrors);
            }
        }

        _logger.LogInformation("Admin user created and assigned Administrator role.");
    }

    /// <summary>
    /// Adds the admin user to all existing organizations so they appear in the
    /// built-in All Users group.
    /// </summary>
    private async Task AddAdminToAllOrganizationsAsync(ApplicationUser user)
    {
        if (_dbContext is null)
        {
            _logger.LogWarning("CoreDbContext not available; skipping organization membership for admin user.");
            return;
        }

        var organizations = await _dbContext.Organizations
            .Where(o => !o.IsDeleted)
            .ToListAsync();

        foreach (var org in organizations)
        {
            var alreadyMember = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == org.Id && m.UserId == user.Id);

            if (alreadyMember)
                continue;

            _dbContext.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = org.Id,
                UserId = user.Id,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        if (organizations.Count > 0)
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation(
                "Added admin user to {Count} organization(s).",
                organizations.Count);
        }
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
    /// Applies the MFA enrollment flag to an existing admin user when:
    /// <list type="bullet">
    ///   <item>MFA was enabled in CLI config (<c>DotNetCloud:AdminMfaEnabled</c>), and</item>
    ///   <item>The user hasn't completed MFA enrollment yet (<c>TwoFactorEnabled</c> is <c>false</c>).</item>
    /// </list>
    /// This handles the case where the admin user was created by an older version,
    /// or where setup was re-run with MFA enabled for an existing installation.
    /// </summary>
    private async Task ApplyMfaSetupFlagAsync(ApplicationUser user)
    {
        var adminMfaEnabled = GetConfigValue("DotNetCloud:AdminMfaEnabled", "enableAdminMfa");
        if (!string.Equals(adminMfaEnabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var twoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        if (twoFactorEnabled)
        {
            return; // MFA already fully set up — nothing to do
        }

        if (user.MfaSetupRequired)
        {
            return; // Flag already set — nothing to do
        }

        user.MfaSetupRequired = true;
        var updateResult = await _userManager.UpdateAsync(user);
        if (updateResult.Succeeded)
        {
            _logger.LogInformation(
                "MFA setup flagged for existing admin user {UserId} (was created before MfaSetupRequired existed).",
                user.Id);
        }
        else
        {
            var errors = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            _logger.LogWarning(
                "Could not flag MFA setup for existing admin user {UserId}: {Errors}",
                user.Id, errors);
        }
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

    /// <summary>
    /// Reads a configuration value with fallback support.
    /// Tries the primary key first (e.g. <c>DotNetCloud:AdminEmail</c> from env vars),
    /// then falls back to a flat key (e.g. <c>adminEmail</c> from config.json).
    /// This ensures the seeder works whether the value comes from environment variables
    /// (set by <c>dotnetcloud start</c>) or from the flat CLI config.json (systemd path).
    /// </summary>
    private string? GetConfigValue(string primaryKey, string fallbackKey)
    {
        var value = _configuration[primaryKey];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = _configuration[fallbackKey];
        return !string.IsNullOrWhiteSpace(value) ? value : null;
    }
}
