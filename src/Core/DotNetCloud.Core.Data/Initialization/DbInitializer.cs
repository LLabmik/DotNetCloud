using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Permissions;
using DotNetCloud.Core.Data.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Data.Initialization;

/// <summary>
/// Provides database initialization and seeding functionality for the DotNetCloud platform.
/// </summary>
/// <remarks>
/// DbInitializer is responsible for:
/// - Creating and migrating the database to the latest schema version
/// - Seeding default org role definitions (Org Admin, Org Manager, Org Member)
/// - Seeding default system settings with recommended configuration values
///
/// This class is typically invoked during the initial setup wizard (dotnetcloud setup)
/// or programmatically during application startup when the database is first created.
///
/// All seeding operations are idempotent - they check for existing data before insertion
/// to prevent duplicate entries and allow safe re-execution.
/// </remarks>
public class DbInitializer
{
    private readonly CoreDbContext _context;
    private readonly ILogger<DbInitializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbInitializer"/> class.
    /// </summary>
    /// <param name="context">The database context to initialize</param>
    /// <param name="logger">Logger for initialization operations</param>
    public DbInitializer(CoreDbContext context, ILogger<DbInitializer> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes the database with schema and default data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <remarks>
    /// This method performs the following operations in order:
    /// 1. Creates database and applies migrations
    /// 2. Seeds default org role definitions
    /// 3. Seeds default system settings
    /// 4. Seeds default organization
    /// 5. Seeds built-in groups
    ///
    /// All operations are wrapped in a transaction (for relational databases) to ensure atomicity.
    /// If any step fails, all changes are rolled back.
    /// </remarks>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting database initialization...");

            // First ensure database exists and is migrated
            await EnsureDatabaseAsync(cancellationToken);

            // Seed default data (transaction only for relational databases)
            var isRelational = _context.Database.IsRelational();

            if (isRelational)
            {
                // Use the execution strategy to wrap the transaction
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                    try
                    {
                        // Seed default data
                        await SeedDefaultRolesAsync(cancellationToken);
                        // Permissions seeding is deferred to a future phase.
                        // The permission entities and RolePermission junction exist as scaffolding
                        // but are not populated or enforced at runtime.
                        // await SeedDefaultPermissionsAsync(cancellationToken);
                        await SeedSystemSettingsAsync(cancellationToken);
                        await SeedDefaultOrganizationAsync(cancellationToken);
                        await SeedBuiltInGroupsAsync(cancellationToken);

                        // Commit transaction
                        await transaction.CommitAsync(cancellationToken);

                        _logger.LogInformation("Database initialization completed successfully.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during database seeding. Rolling back transaction.");
                        await transaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                });
            }
            else
            {
                // For in-memory databases, seed without transaction
                await SeedDefaultRolesAsync(cancellationToken);
                // Permissions seeding is deferred to a future phase.
                // await SeedDefaultPermissionsAsync(cancellationToken);
                await SeedSystemSettingsAsync(cancellationToken);
                await SeedDefaultOrganizationAsync(cancellationToken);
                await SeedBuiltInGroupsAsync(cancellationToken);

                _logger.LogInformation("Database initialization completed successfully (in-memory).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed.");
            throw;
        }
    }

    /// <summary>
    /// Ensures the database exists and applies all pending migrations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring database exists and is migrated to latest version...");

        // Check if the database provider supports migrations (i.e., not in-memory)
        var isRelational = _context.Database.IsRelational();

        if (isRelational)
        {
            // First, verify the database exists. MigrateAsync will try to CREATE DATABASE
            // if it doesn't exist, which requires elevated permissions that the runtime user
            // may not have. Check connectivity first to give a clear error message.
            var dbExists = await _context.Database.CanConnectAsync(cancellationToken);

            if (!dbExists)
            {
                // Try to create the database (requires CREATEDB on PostgreSQL,
                // CREATE DATABASE on SQL Server, etc.)
                try
                {
                    _logger.LogInformation("Database does not exist — attempting to create...");
                    await _context.Database.EnsureCreatedAsync(cancellationToken);
                    _logger.LogInformation("Database created successfully.");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "The database does not exist and the configured user lacks permission to create it. "
                        + "Create the database manually or run 'dotnetcloud setup' to configure it. "
                        + $"Underlying error: {ex.Message}", ex);
                }
            }

            // Check if there are any pending migrations
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);

            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations...", pendingMigrations.Count());

                // Apply all pending migrations
                await _context.Database.MigrateAsync(cancellationToken);

                _logger.LogInformation("Migrations applied successfully.");
            }
            else
            {
                _logger.LogInformation("Database is up to date. No pending migrations.");
            }
        }
        else
        {
            // For in-memory databases, just ensure created
            await _context.Database.EnsureCreatedAsync(cancellationToken);
            _logger.LogInformation("In-memory database created.");
        }

        // Verify database can be accessed
        var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            throw new InvalidOperationException("Cannot connect to database after migration.");
        }
    }

    /// <summary>
    /// Seeds default org role definitions into the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <remarks>
    /// Creates the three organization-scoped roles using well-known GUIDs:
    /// - Org Admin: Full control over an organization including members, teams, and module data.
    /// - Org Manager: Can create and edit organization resources and manage teams.
    /// - Org Member: Default read-level access to view and consume organization resources.
    ///
    /// These roles are stored in the Permissions.Role table (distinct from AspNetRoles).
    /// The GUIDs are referenced by OrganizationMember.RoleIds at runtime.
    ///
    /// Uses upsert-by-ID: existing rows with matching well-known GUIDs are updated in place.
    /// Roles from the old seed set (Administrator/User/Guest/Moderator with random GUIDs)
    /// are kept but no longer qualify as well-known org roles.
    /// </remarks>
    private async Task SeedDefaultRolesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Seeding default org roles...");

        var wellKnownIds = OrgRoleIds.All.ToHashSet();

        // Fetch any existing roles that overlap with well-known GUIDs
        var existingRoles = await _context.Roles
            .Where(r => wellKnownIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var newRoles = new List<Role>();
        var updatedCount = 0;

        var definitions = new[]
        {
            (Id: OrgRoleIds.OrgAdmin, Name: "Org Admin", Description: "Full control over an organization including members, teams, groups, and all module data."),
            (Id: OrgRoleIds.OrgManager, Name: "Org Manager", Description: "Can create and edit organization resources such as calendars, shared folders, and manage teams."),
            (Id: OrgRoleIds.OrgMember, Name: "Org Member", Description: "Can view and use organization resources. Default role for new members.")
        };

        foreach (var def in definitions)
        {
            if (existingRoles.TryGetValue(def.Id, out var existing))
            {
                if (existing.Name != def.Name || existing.Description != def.Description || !existing.IsSystemRole)
                {
                    existing.Name = def.Name;
                    existing.Description = def.Description;
                    existing.IsSystemRole = true;
                    updatedCount++;
                }
            }
            else
            {
                newRoles.Add(new Role
                {
                    Id = def.Id,
                    Name = def.Name,
                    Description = def.Description,
                    IsSystemRole = true
                });
            }
        }

        if (newRoles.Count > 0)
        {
            await _context.Roles.AddRangeAsync(newRoles, cancellationToken);
        }

        if (newRoles.Count > 0 || updatedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {New} new org roles, updated {Updated} existing.", newRoles.Count, updatedCount);
        }
        else
        {
            _logger.LogInformation("All well-known org roles already present and up-to-date.");
        }
    }

    /// <summary>
    /// Seeds default permissions into the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <remarks>
    /// Creates permissions for all core modules following a hierarchical naming convention.
    /// Permissions are organized by module and action type (e.g., "files.upload", "users.create").
    /// 
    /// Core modules include:
    /// - Core: System administration and user management
    /// - Files: File storage and sharing
    /// - Chat: Messaging and notifications
    /// - Calendar: Event and scheduling
    /// - Contacts: Contact management
    /// - Notes: Note-taking and documentation
    /// 
    /// This operation is idempotent - existing permissions are not duplicated.
    /// </remarks>
    private async Task SeedDefaultPermissionsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Seeding default permissions...");

        // Check if permissions already exist
        var existingPermissionsCount = await _context.Permissions.CountAsync(cancellationToken);
        if (existingPermissionsCount > 0)
        {
            _logger.LogInformation("Permissions already exist. Skipping permission seeding.");
            return;
        }

        var defaultPermissions = new List<Permission>();

        // Core module permissions
        defaultPermissions.AddRange(new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "core.admin", DisplayName = "System Administration", Description = "Full administrative access to system configuration and settings." },
            new Permission { Id = Guid.NewGuid(), Code = "core.users.view", DisplayName = "View Users", Description = "View user accounts and profiles." },
            new Permission { Id = Guid.NewGuid(), Code = "core.users.create", DisplayName = "Create Users", Description = "Create new user accounts." },
            new Permission { Id = Guid.NewGuid(), Code = "core.users.edit", DisplayName = "Edit Users", Description = "Modify existing user accounts and profiles." },
            new Permission { Id = Guid.NewGuid(), Code = "core.users.delete", DisplayName = "Delete Users", Description = "Delete user accounts." },
            new Permission { Id = Guid.NewGuid(), Code = "core.roles.view", DisplayName = "View Roles", Description = "View role definitions and assignments." },
            new Permission { Id = Guid.NewGuid(), Code = "core.roles.create", DisplayName = "Create Roles", Description = "Create new custom roles." },
            new Permission { Id = Guid.NewGuid(), Code = "core.roles.edit", DisplayName = "Edit Roles", Description = "Modify existing custom roles." },
            new Permission { Id = Guid.NewGuid(), Code = "core.roles.delete", DisplayName = "Delete Roles", Description = "Delete custom roles." },
            new Permission { Id = Guid.NewGuid(), Code = "core.settings.view", DisplayName = "View Settings", Description = "View system and module settings." },
            new Permission { Id = Guid.NewGuid(), Code = "core.settings.edit", DisplayName = "Edit Settings", Description = "Modify system and module settings." },
            new Permission { Id = Guid.NewGuid(), Code = "core.modules.view", DisplayName = "View Modules", Description = "View installed modules and their status." },
            new Permission { Id = Guid.NewGuid(), Code = "core.modules.manage", DisplayName = "Manage Modules", Description = "Install, uninstall, start, and stop modules." }
        });

        // Files module permissions
        defaultPermissions.AddRange(new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "files.view", DisplayName = "View Files", Description = "View files and folders." },
            new Permission { Id = Guid.NewGuid(), Code = "files.upload", DisplayName = "Upload Files", Description = "Upload new files." },
            new Permission { Id = Guid.NewGuid(), Code = "files.download", DisplayName = "Download Files", Description = "Download files." },
            new Permission { Id = Guid.NewGuid(), Code = "files.edit", DisplayName = "Edit Files", Description = "Edit file content and metadata." },
            new Permission { Id = Guid.NewGuid(), Code = "files.delete", DisplayName = "Delete Files", Description = "Delete files and folders." },
            new Permission { Id = Guid.NewGuid(), Code = "files.share", DisplayName = "Share Files", Description = "Share files with other users or publicly." },
            new Permission { Id = Guid.NewGuid(), Code = "files.versions", DisplayName = "Manage File Versions", Description = "View and restore file versions." }
        });

        // Chat module permissions
        defaultPermissions.AddRange(new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "chat.send", DisplayName = "Send Messages", Description = "Send messages in channels and direct messages." },
            new Permission { Id = Guid.NewGuid(), Code = "chat.read", DisplayName = "Read Messages", Description = "Read messages in channels and direct messages." },
            new Permission { Id = Guid.NewGuid(), Code = "chat.channels.create", DisplayName = "Create Channels", Description = "Create new chat channels." },
            new Permission { Id = Guid.NewGuid(), Code = "chat.channels.edit", DisplayName = "Edit Channels", Description = "Modify channel settings and members." },
            new Permission { Id = Guid.NewGuid(), Code = "chat.channels.delete", DisplayName = "Delete Channels", Description = "Delete chat channels." },
            new Permission { Id = Guid.NewGuid(), Code = "chat.moderate", DisplayName = "Moderate Chat", Description = "Delete messages and manage channel behavior." }
        });

        // Calendar module permissions
        defaultPermissions.AddRange(new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "calendar.view", DisplayName = "View Calendar", Description = "View calendar events." },
            new Permission { Id = Guid.NewGuid(), Code = "calendar.create", DisplayName = "Create Events", Description = "Create new calendar events." },
            new Permission { Id = Guid.NewGuid(), Code = "calendar.edit", DisplayName = "Edit Events", Description = "Modify existing calendar events." },
            new Permission { Id = Guid.NewGuid(), Code = "calendar.delete", DisplayName = "Delete Events", Description = "Delete calendar events." },
            new Permission { Id = Guid.NewGuid(), Code = "calendar.share", DisplayName = "Share Calendar", Description = "Share calendar with other users." }
        });

        // Contacts module permissions
        defaultPermissions.AddRange(new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "contacts.view", DisplayName = "View Contacts", Description = "View contact information." },
            new Permission { Id = Guid.NewGuid(), Code = "contacts.create", DisplayName = "Create Contacts", Description = "Create new contacts." },
            new Permission { Id = Guid.NewGuid(), Code = "contacts.edit", DisplayName = "Edit Contacts", Description = "Modify existing contacts." },
            new Permission { Id = Guid.NewGuid(), Code = "contacts.delete", DisplayName = "Delete Contacts", Description = "Delete contacts." },
            new Permission { Id = Guid.NewGuid(), Code = "contacts.share", DisplayName = "Share Contacts", Description = "Share contacts with other users." }
        });

        // Notes module permissions
        defaultPermissions.AddRange(new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "notes.view", DisplayName = "View Notes", Description = "View notes and documents." },
            new Permission { Id = Guid.NewGuid(), Code = "notes.create", DisplayName = "Create Notes", Description = "Create new notes." },
            new Permission { Id = Guid.NewGuid(), Code = "notes.edit", DisplayName = "Edit Notes", Description = "Modify existing notes." },
            new Permission { Id = Guid.NewGuid(), Code = "notes.delete", DisplayName = "Delete Notes", Description = "Delete notes." },
            new Permission { Id = Guid.NewGuid(), Code = "notes.share", DisplayName = "Share Notes", Description = "Share notes with other users." }
        });

        await _context.Permissions.AddRangeAsync(defaultPermissions, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} default permissions.", defaultPermissions.Count);
    }

    /// <summary>
    /// Seeds default system settings into the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <remarks>
    /// Creates default system-wide settings with recommended values for:
    /// - Core system behavior (session timeout, registration, security)
    /// - File storage limits and behavior
    /// - Notification settings
    /// - Backup configuration
    /// 
    /// These settings can be modified by administrators after initialization.
    /// This operation is idempotent - existing settings are not duplicated.
    /// </remarks>
    private async Task SeedSystemSettingsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Seeding default system settings...");

        var defaultSettings = new[]
        {
            // Core system settings
            new SystemSetting
            {
                Module = "dotnetcloud.core",
                Key = "SessionTimeout",
                Value = "3600",
                Description = "Session timeout in seconds (default: 1 hour)"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.core",
                Key = "EnableRegistration",
                Value = "true",
                Description = "Allow new user registration"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.core",
                Key = "RequireEmailVerification",
                Value = "true",
                Description = "Require email verification for new accounts"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.core",
                Key = "PasswordMinLength",
                Value = "8",
                Description = "Minimum password length"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.core",
                Key = "PasswordRequireUppercase",
                Value = "true",
                Description = "Require uppercase letters in passwords"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.core",
                Key = "PasswordRequireDigit",
                Value = "true",
                Description = "Require digits in passwords"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.core",
                Key = "PasswordRequireNonAlphanumeric",
                Value = "true",
                Description = "Require special characters in passwords"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.core",
                Key = "MaxLoginAttempts",
                Value = "5",
                Description = "Maximum failed login attempts before account lockout"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.core",
                Key = "LockoutDurationMinutes",
                Value = "15",
                Description = "Account lockout duration in minutes after max login attempts"
            },

            // Files module settings
            new SystemSetting
            {
                Module = "dotnetcloud.files",
                Key = "MaxUploadSizeBytes",
                Value = "104857600",
                Description = "Maximum file upload size in bytes (default: 100 MB)"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.files",
                Key = "EnableVersioning",
                Value = "true",
                Description = "Enable file versioning"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.files",
                Key = "MaxVersionsPerFile",
                Value = "10",
                Description = "Maximum number of versions to keep per file"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.files",
                Key = "EnableDeduplication",
                Value = "true",
                Description = "Enable file deduplication to save storage"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.files",
                Key = "DefaultQuotaGB",
                Value = "10",
                Description = "Default storage quota per user in GB"
            },

            // Notifications module settings
            new SystemSetting
            {
                Module = "dotnetcloud.notifications",
                Key = "EmailEnabled",
                Value = "false",
                Description = "Enable email notifications (requires SMTP configuration)"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.notifications",
                Key = "PushEnabled",
                Value = "false",
                Description = "Enable push notifications to mobile devices"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.notifications",
                Key = "EmailProvider",
                Value = "smtp",
                Description = "Email provider type (smtp, sendgrid, mailgun)"
            },

            // Backup settings
            new SystemSetting
            {
                Module = "dotnetcloud.backup",
                Key = "EnableAutoBackup",
                Value = "false",
                Description = "Enable automatic backups"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.backup",
                Key = "BackupSchedule",
                Value = "0 2 * * *",
                Description = "Backup schedule in cron format (default: 2 AM daily)"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.backup",
                Key = "BackupRetentionDays",
                Value = "30",
                Description = "Number of days to retain backups"
            },

            // Security settings
            new SystemSetting
            {
                Module = "dotnetcloud.security",
                Key = "EnableTwoFactor",
                Value = "true",
                Description = "Allow users to enable two-factor authentication"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.security",
                Key = "RequireTwoFactorForAdmins",
                Value = "true",
                Description = "Require two-factor authentication for administrator accounts"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.security",
                Key = "EnableWebAuthn",
                Value = "true",
                Description = "Enable WebAuthn/Passkey support"
            },

            // AI Assistant module settings
            new SystemSetting
            {
                Module = "dotnetcloud.ai",
                Key = "Provider",
                Value = "ollama",
                Description = "LLM provider type (ollama, openai, anthropic)"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.ai",
                Key = "ApiBaseUrl",
                Value = "http://localhost:11434/",
                Description = "Base URL of the LLM API endpoint"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.ai",
                Key = "ApiKey",
                Value = "",
                Description = "API key for cloud LLM providers (OpenAI, Anthropic). Not required for local Ollama."
            },
            new SystemSetting
            {
                Module = "dotnetcloud.ai",
                Key = "OrganizationId",
                Value = "",
                Description = "Optional organization ID for providers that support it (e.g. OpenAI)"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.ai",
                Key = "DefaultModel",
                Value = "gpt-oss:20b",
                Description = "Default LLM model for new conversations"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.ai",
                Key = "MaxTokens",
                Value = "0",
                Description = "Maximum tokens per response (0 = provider default)"
            },
            new SystemSetting
            {
                Module = "dotnetcloud.ai",
                Key = "RequestTimeoutSeconds",
                Value = "300",
                Description = "Request timeout in seconds for LLM API calls"
            }
        };

        // Load existing setting keys to avoid duplicates
        var existingKeys = await _context.SystemSettings
            .Select(s => s.Module + "|" + s.Key)
            .ToHashSetAsync(cancellationToken);

        var newSettings = defaultSettings
            .Where(s => !existingKeys.Contains(s.Module + "|" + s.Key))
            .ToArray();

        if (newSettings.Length == 0)
        {
            _logger.LogInformation("All default system settings already exist. Nothing to seed.");
            return;
        }

        await _context.SystemSettings.AddRangeAsync(newSettings, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} new system settings ({Existing} already existed).",
            newSettings.Length, defaultSettings.Length - newSettings.Length);
    }

    /// <summary>
    /// Seeds a default organization if none exists.
    /// Every DotNetCloud instance requires at least one organization for team creation.
    /// </summary>
    private async Task SeedDefaultOrganizationAsync(CancellationToken cancellationToken)
    {
        var exists = await _context.Organizations.AnyAsync(cancellationToken);
        if (exists)
        {
            _logger.LogInformation("Organizations already exist. Skipping default organization seeding.");
            return;
        }

        var org = new Organization
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Default",
            Description = "Default organization",
            CreatedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(org);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded default organization '{Name}' ({Id}).", org.Name, org.Id);
    }

    /// <summary>
    /// Ensures every organization has exactly one built-in <c>All Users</c> group.
    /// </summary>
    private async Task SeedBuiltInGroupsAsync(CancellationToken cancellationToken)
    {
        var organizations = await _context.Organizations
            .Where(o => !o.IsDeleted)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        if (organizations.Count == 0)
        {
            _logger.LogInformation("No organizations available. Skipping built-in group seeding.");
            return;
        }

        var groups = await _context.Groups
            .Where(g => organizations.Select(o => o.Id).Contains(g.OrganizationId))
            .ToListAsync(cancellationToken);

        var createdCount = 0;
        var convertedCount = 0;

        foreach (var organization in organizations)
        {
            if (groups.Any(group => group.OrganizationId == organization.Id && group.IsAllUsersGroup))
                continue;

            var existingNamedGroup = groups.FirstOrDefault(group =>
                group.OrganizationId == organization.Id &&
                string.Equals(group.Name, Group.AllUsersGroupName, StringComparison.OrdinalIgnoreCase));

            if (existingNamedGroup is not null)
            {
                existingNamedGroup.IsAllUsersGroup = true;
                existingNamedGroup.Name = Group.AllUsersGroupName;
                existingNamedGroup.Description ??= "Built-in group containing all active organization members.";
                convertedCount++;
                continue;
            }

            var builtInGroup = new Group
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                Name = Group.AllUsersGroupName,
                Description = "Built-in group containing all active organization members.",
                IsAllUsersGroup = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Groups.Add(builtInGroup);
            groups.Add(builtInGroup);
            createdCount++;
        }

        if (createdCount == 0 && convertedCount == 0)
        {
            _logger.LogInformation("Built-in All Users groups already exist for all organizations.");
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Ensured built-in All Users groups for organizations ({Created} created, {Converted} backfilled).",
            createdCount,
            convertedCount);
    }
}
