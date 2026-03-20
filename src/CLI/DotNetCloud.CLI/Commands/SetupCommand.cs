using System.CommandLine;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DotNetCloud.CLI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Interactive first-run wizard: database selection, connection string,
/// admin user creation, organization setup, TLS configuration, and module selection.
/// </summary>
internal static class SetupCommand
{
    private const int TotalSteps = 9;

    /// <summary>
    /// Creates the <c>setup</c> command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("setup", "Interactive first-run setup wizard");
        command.SetAction(_ => RunSetupWizardAsync());
        return command;
    }

    private static async Task<int> RunSetupWizardAsync()
    {
        ConsoleOutput.WriteHeader("DotNetCloud Setup Wizard");

        // Load existing config so previous values become defaults.
        // On first run this returns a fresh CliConfig with built-in defaults.
        var existing = CliConfiguration.ConfigExists() ? CliConfiguration.Load() : null;
        var isRerun = existing?.SetupCompletedAt is not null;

        if (isRerun)
        {
            ConsoleOutput.WriteInfo("Previous configuration detected — previous values shown as defaults.");
            ConsoleOutput.WriteInfo("Press Enter to keep the existing value, or type a new one.");
            Console.WriteLine();
        }

        // Start from existing config (preserves values the wizard doesn't ask about)
        // or a fresh config on first run.
        var config = existing ?? new CliConfig();

        // ───────────────────────────────────────────────
        // Step 1: Database
        // ───────────────────────────────────────────────
        ConsoleOutput.WriteStep(1, TotalSteps, "Database Configuration");
        var previousDbIndex = config.DatabaseProvider switch
        {
            "PostgreSQL" => 0,
            "SqlServer" => 1,
            "MariaDB" => 2,
            _ => -1
        };

        var dbChoice = ConsoleOutput.PromptChoice(
            "Select database provider:",
            ["PostgreSQL (recommended)", "SQL Server", "MariaDB"],
            defaultIndex: previousDbIndex);

        config.DatabaseProvider = dbChoice switch
        {
            0 => "PostgreSQL",
            1 => "SqlServer",
            2 => "MariaDB",
            _ => "PostgreSQL"
        };

        // On Linux, offer to install PostgreSQL if it's not present
        string? dbPassword = null;
        if (config.DatabaseProvider == "PostgreSQL"
            && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !DatabaseSetupHelper.IsPostgreSqlInstalled())
        {
            Console.WriteLine();
            ConsoleOutput.WriteWarning("PostgreSQL is not installed on this system.");

            if (ConsoleOutput.PromptConfirm("Install PostgreSQL now?", defaultValue: true))
            {
                if (!DatabaseSetupHelper.InstallPostgreSql())
                {
                    ConsoleOutput.WriteError("PostgreSQL installation failed.");
                    ConsoleOutput.WriteInfo("Install manually: sudo apt-get install postgresql");
                    if (!ConsoleOutput.PromptConfirm("Continue setup anyway?"))
                    {
                        return 1;
                    }
                }
            }
        }

        // Guided connection string builder vs. raw string
        var providerChanged = previousDbIndex != dbChoice;
        var hasExistingConnStr = !string.IsNullOrWhiteSpace(config.ConnectionString) && !providerChanged;

        if (hasExistingConnStr)
        {
            // Re-run with same provider — offer the saved connection string
            config.ConnectionString = ConsoleOutput.Prompt("Connection string", config.ConnectionString);
        }
        else
        {
            // First run or provider changed — guided mode
            var inputMode = ConsoleOutput.PromptChoice(
                "How would you like to configure the database connection?",
                ["Guided (answer a few simple questions)", "Advanced (enter full connection string)"],
                defaultIndex: 0);

            if (inputMode == 1)
            {
                // Advanced: raw connection string
                var defaultConnStr = config.DatabaseProvider switch
                {
                    "PostgreSQL" => "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=yourpassword",
                    "SqlServer" => "Server=localhost;Database=dotnetcloud;Trusted_Connection=True;TrustServerCertificate=True",
                    "MariaDB" => "Server=localhost;Database=dotnetcloud;User=dotnetcloud;Password=yourpassword",
                    _ => ""
                };
                config.ConnectionString = ConsoleOutput.Prompt("Connection string", defaultConnStr);
            }
            else
            {
                // Guided mode
                config.ConnectionString = BuildConnectionStringGuided(config.DatabaseProvider, out dbPassword);
            }
        }

        // Step 2: Verify database connection
        ConsoleOutput.WriteStep(2, TotalSteps, "Verifying Database Connection");
        var canConnect = await VerifyDatabaseConnectionAsync(config);
        if (!canConnect)
        {
            ConsoleOutput.WriteError("Could not connect to the database.");

            // If PostgreSQL on Linux with guided mode, offer to create the DB
            if (config.DatabaseProvider == "PostgreSQL"
                && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && dbPassword is not null)
            {
                Console.WriteLine();
                ConsoleOutput.WriteInfo("The database or user may not exist yet.");
                if (ConsoleOutput.PromptConfirm("Create the database and user now?", defaultValue: true))
                {
                    // Extract parts from connection string
                    var parts = ParseConnectionString(config.ConnectionString);
                    var dbName = parts.GetValueOrDefault("database", "dotnetcloud");
                    var dbUser = parts.GetValueOrDefault("username", "dotnetcloud");

                    if (DatabaseSetupHelper.CreatePostgreSqlDatabase(dbName, dbUser, dbPassword))
                    {
                        ConsoleOutput.WriteSuccess("Database and user created successfully.");
                        canConnect = await VerifyDatabaseConnectionAsync(config);
                    }
                    else
                    {
                        ConsoleOutput.WriteWarning("Automatic database creation failed.");
                        ConsoleOutput.WriteInfo("Create manually:");
                        Console.WriteLine($"    sudo -u postgres createuser {dbUser}");
                        Console.WriteLine($"    sudo -u postgres createdb -O {dbUser} {dbName}");
                        Console.WriteLine($"    sudo -u postgres psql -c \"ALTER USER {dbUser} PASSWORD '<password>';\"");
                    }
                }
            }

            if (!canConnect && !ConsoleOutput.PromptConfirm("Continue anyway?"))
            {
                return 1;
            }

            if (canConnect)
            {
                ConsoleOutput.WriteSuccess("Database connection verified.");
            }
        }
        else
        {
            ConsoleOutput.WriteSuccess("Database connection verified.");
        }

        // ───────────────────────────────────────────────
        // Step 3: Admin user
        // ───────────────────────────────────────────────
        ConsoleOutput.WriteStep(3, TotalSteps, "Admin User Configuration");
        Console.WriteLine();
        ConsoleOutput.WriteInfo("Your email address will be your login username for the DotNetCloud web UI.");
        ConsoleOutput.WriteInfo("It must be a valid email address (e.g., admin@example.com).");
        ConsoleOutput.WriteInfo("This is separate from the database credentials you entered above.");
        Console.WriteLine();

        config.AdminEmail = ConsoleOutput.Prompt(
            "Admin email (this is your login username)",
            config.AdminEmail);

        // Password entry with retry loop and strength validation
        Console.WriteLine();
        ConsoleOutput.WriteInfo("Choose a strong password for your admin account.");
        ConsoleOutput.WriteInfo("This is NOT the same as the database password you entered earlier.");
        Console.WriteLine();

        string adminPassword;
        while (true)
        {
            adminPassword = ConsoleOutput.PromptPassword("Admin password");

            // Validate strength (silently rejects if it matches the DB password)
            var validationError = PasswordValidator.Validate(adminPassword, dbPassword ?? "");
            if (validationError is not null)
            {
                ConsoleOutput.WriteError(validationError);
                ConsoleOutput.WriteInfo("Please try again.");
                Console.WriteLine();
                continue;
            }

            var confirmPassword = ConsoleOutput.PromptPassword("Confirm admin password");
            if (adminPassword != confirmPassword)
            {
                ConsoleOutput.WriteError("Passwords do not match. Please try again.");
                Console.WriteLine();
                continue;
            }

            break;
        }

        // ───────────────────────────────────────────────
        // Step 4: MFA setup prompt
        // ───────────────────────────────────────────────
        ConsoleOutput.WriteStep(4, TotalSteps, "Multi-Factor Authentication");
        var enableMfa = ConsoleOutput.PromptConfirm("Enable TOTP MFA for admin account?", defaultValue: true);
        if (enableMfa)
        {
            ConsoleOutput.WriteInfo("MFA will be configured on first login via the web UI.");
        }

        // ───────────────────────────────────────────────
        // Step 5: Organization
        // ───────────────────────────────────────────────
        ConsoleOutput.WriteStep(5, TotalSteps, "Organization Setup");
        config.OrganizationName = ConsoleOutput.Prompt(
            "Organization name",
            !string.IsNullOrWhiteSpace(config.OrganizationName) ? config.OrganizationName : "My Organization");

        // ───────────────────────────────────────────────
        // Step 6: TLS/HTTPS
        // ───────────────────────────────────────────────
        ConsoleOutput.WriteStep(6, TotalSteps, "TLS/HTTPS Configuration");
        config.EnableHttps = ConsoleOutput.PromptConfirm("Enable HTTPS?", defaultValue: config.EnableHttps);

        if (config.EnableHttps)
        {
            config.HttpsPort = int.TryParse(
                ConsoleOutput.Prompt("HTTPS port", config.HttpsPort.ToString()),
                out var httpsPort) ? httpsPort : 5443;

            var tlsModeDefaultIndex = config.UseLetsEncrypt
                ? 0
                : config.UseSelfSignedTls ? 1 : 2;

            var tlsModeChoice = ConsoleOutput.PromptChoice(
                "TLS certificate mode:",
                [
                    "Public internet (Let's Encrypt automatic certificates)",
                    "Private testing (generate self-signed certificate)",
                    "Use existing certificate file (PFX/PEM)"
                ],
                defaultIndex: tlsModeDefaultIndex);

            if (tlsModeChoice == 0)
            {
                config.UseLetsEncrypt = true;
                config.UseSelfSignedTls = false;
                config.LetsEncryptDomain = ConsoleOutput.Prompt(
                    "Domain name (e.g., cloud.example.com)",
                    config.LetsEncryptDomain);
            }
            else if (tlsModeChoice == 1)
            {
                config.UseLetsEncrypt = false;
                config.UseSelfSignedTls = true;
                config.LetsEncryptDomain = null;

                var defaultSelfSignedHost = !string.IsNullOrWhiteSpace(config.SelfSignedTlsHost)
                    ? config.SelfSignedTlsHost
                    : Environment.MachineName;

                config.SelfSignedTlsHost = ConsoleOutput.Prompt(
                    "Hostname/IP for private certificate (LAN name or local IP)",
                    defaultSelfSignedHost);

                if (string.IsNullOrWhiteSpace(config.TlsCertificatePath))
                {
                    var certDir = CliConfiguration.IsSystemInstall
                        ? Path.Combine(CliConfiguration.GetConfigDirectory(), "certs")
                        : Path.Combine(config.DataDirectory, "certs");

                    config.TlsCertificatePath = Path.Combine(certDir, "dotnetcloud-selfsigned.pfx");
                }
            }
            else
            {
                config.UseLetsEncrypt = false;
                config.UseSelfSignedTls = false;
                config.LetsEncryptDomain = null;
                config.SelfSignedTlsHost = null;
                config.TlsCertificatePath = ConsoleOutput.Prompt(
                    "Path to TLS certificate (PFX or PEM)",
                    config.TlsCertificatePath ?? "");
            }
        }

        config.HttpPort = int.TryParse(
            ConsoleOutput.Prompt("HTTP port", config.HttpPort.ToString()),
            out var httpPort) ? httpPort : 5080;

        // ───────────────────────────────────────────────
        // Step 7: Module selection
        // ───────────────────────────────────────────────
        ConsoleOutput.WriteStep(7, TotalSteps, "Module Selection");

        // Files and Chat are required core modules — always enabled.
        var requiredModules = new[] { "dotnetcloud.files", "dotnetcloud.chat" };
        var optionalModules = new[]
        {
            "dotnetcloud.contacts",
            "dotnetcloud.calendar",
            "dotnetcloud.notes",
            "dotnetcloud.deck"
        };

        var previouslyEnabled = config.EnabledModules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        config.EnabledModules.Clear();

        foreach (var moduleId in requiredModules)
        {
            config.EnabledModules.Add(moduleId);
        }

        ConsoleOutput.WriteInfo("Required modules (always enabled): dotnetcloud.files, dotnetcloud.chat");

        if (optionalModules.Length > 0)
        {
            ConsoleOutput.WriteInfo("Select optional modules to enable (more can be enabled later):");
            foreach (var moduleId in optionalModules)
            {
                var wasEnabled = previouslyEnabled.Contains(moduleId);

                if (ConsoleOutput.PromptConfirm($"  Enable {moduleId}?", defaultValue: wasEnabled))
                {
                    config.EnabledModules.Add(moduleId);
                }
            }
        }

        // ───────────────────────────────────────────────
        // Step 8: Data directories
        // ───────────────────────────────────────────────
        ConsoleOutput.WriteStep(8, TotalSteps, "Data Directories");

        var customizeDirs = ConsoleOutput.PromptConfirm("Customize data directories?");
        if (customizeDirs)
        {
            config.DataDirectory = ConsoleOutput.Prompt("Data directory", config.DataDirectory);
            config.LogDirectory = ConsoleOutput.Prompt("Log directory", config.LogDirectory);
            config.BackupDirectory = ConsoleOutput.Prompt("Backup directory", config.BackupDirectory);
        }
        else
        {
            ConsoleOutput.WriteInfo("Using default directories:");
            ConsoleOutput.WriteDetail("Data", config.DataDirectory);
            ConsoleOutput.WriteDetail("Logs", config.LogDirectory);
            ConsoleOutput.WriteDetail("Backups", config.BackupDirectory);
            Console.WriteLine();

            if (CliConfiguration.IsSystemInstall)
            {
                ConsoleOutput.WriteInfo("These follow the Linux Filesystem Hierarchy Standard (FHS):");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("    /var/lib/dotnetcloud  — Persistent data (files, database dumps)");
                Console.WriteLine("    /var/log/dotnetcloud  — Log files (rotated automatically)");
                Console.WriteLine("    /var/lib/dotnetcloud/backups — Scheduled and manual backups");
                Console.ResetColor();
            }
            else
            {
                ConsoleOutput.WriteInfo("Stored under your user profile (portable, no root required).");
            }
        }

        // ───────────────────────────────────────────────
        // Step 9: Collabora Online / document editing
        // ───────────────────────────────────────────────
        ConsoleOutput.WriteStep(9, TotalSteps, "Collabora Online (Document Editing)");
        ConsoleOutput.WriteInfo("Collabora Online enables in-browser editing of Word, Excel, and PowerPoint files.");

        if (config.EnabledModules.Contains("dotnetcloud.files"))
        {
            var previousCollabora = config.CollaboraMode is "BuiltIn" or "External";
            var enableCollabora = ConsoleOutput.PromptConfirm(
                "Enable Collabora Online document editing?",
                defaultValue: previousCollabora);

            if (enableCollabora)
            {
                var previousCollaboraIndex = config.CollaboraMode switch
                {
                    "BuiltIn" => 0,
                    "External" => 1,
                    _ => -1
                };

                var collaboraChoice = ConsoleOutput.PromptChoice(
                    "Collabora Online installation:",
                    [
                        "Install built-in Collabora CODE automatically (recommended for small instances)",
                        "Connect to an existing Collabora Online server (for large deployments)"
                    ],
                    defaultIndex: previousCollaboraIndex);

                if (collaboraChoice == 0)
                {
                    config.CollaboraMode = "BuiltIn";
                    ConsoleOutput.WriteSuccess("Collabora CODE will be installed automatically.");
                    ConsoleOutput.WriteInfo("If installation fails later, you can retry with:");
                    Console.WriteLine("    sudo dotnetcloud collabora-install");
                    ConsoleOutput.WriteInfo("Or skip it entirely — document editing is optional.");
                    ConsoleOutput.WriteInfo("You can always enable it later in the admin settings.");
                }
                else
                {
                    config.CollaboraMode = "External";
                    config.CollaboraUrl = ConsoleOutput.Prompt(
                        "Collabora Online server URL",
                        !string.IsNullOrWhiteSpace(config.CollaboraUrl)
                            ? config.CollaboraUrl
                            : "https://collabora.example.com");

                    ConsoleOutput.WriteInfo("Make sure the Collabora server can reach this DotNetCloud instance");
                    ConsoleOutput.WriteInfo("over the network (for WOPI file access).");
                }
            }
            else
            {
                config.CollaboraMode = "None";
                ConsoleOutput.WriteInfo("You can enable document editing later via the admin UI");
                ConsoleOutput.WriteInfo("or by running: sudo dotnetcloud collabora-install");
            }
        }
        else
        {
            ConsoleOutput.WriteInfo("Skipped (dotnetcloud.files module not enabled).");
            ConsoleOutput.WriteInfo("Enable the Files module first, then run setup again to configure Collabora.");
        }

        // ═══════════════════════════════════════════════
        // Summary
        // ═══════════════════════════════════════════════
        Console.WriteLine();
        ConsoleOutput.WriteHeader("Configuration Summary");
        ConsoleOutput.WriteDetail("Database", config.DatabaseProvider);
        ConsoleOutput.WriteDetail("Connection", MaskConnectionString(config.ConnectionString));
        ConsoleOutput.WriteDetail("Admin Login", config.AdminEmail ?? "(not set)");
        ConsoleOutput.WriteDetail("Organization", config.OrganizationName ?? "(not set)");
        ConsoleOutput.WriteDetail("HTTPS", config.EnableHttps ? $"Enabled (port {config.HttpsPort})" : "Disabled");
        if (config.EnableHttps)
        {
            var tlsMode = config.UseLetsEncrypt
                ? "Let's Encrypt (public)"
                : config.UseSelfSignedTls ? "Self-signed (private testing)" : "Existing certificate";
            ConsoleOutput.WriteDetail("TLS Mode", tlsMode);
        }
        ConsoleOutput.WriteDetail("HTTP Port", config.HttpPort.ToString());
        ConsoleOutput.WriteDetail("Modules", config.EnabledModules.Count > 0
            ? string.Join(", ", config.EnabledModules) : "(none)");
        ConsoleOutput.WriteDetail("Data Dir", config.DataDirectory);
        ConsoleOutput.WriteDetail("Log Dir", config.LogDirectory);
        ConsoleOutput.WriteDetail("Backup Dir", config.BackupDirectory);
        ConsoleOutput.WriteDetail("Collabora", config.CollaboraMode switch
        {
            "BuiltIn" => "Built-in CODE (auto-installed)",
            "External" => $"External ({config.CollaboraUrl})",
            _ => "Disabled"
        });

        Console.WriteLine();
        if (!ConsoleOutput.PromptConfirm("Save this configuration?", defaultValue: true))
        {
            ConsoleOutput.WriteInfo("Setup cancelled.");
            return 0;
        }

        // ═══════════════════════════════════════════════
        // Save & post-setup
        // ═══════════════════════════════════════════════
        config.SetupCompletedAt = DateTime.UtcNow;

        try
        {
            CliConfiguration.Save(config);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine();
            ConsoleOutput.WriteError(
                $"Permission denied writing to '{CliConfiguration.GetConfigFilePath()}'.");
            ConsoleOutput.WriteInfo("Re-run with sudo: sudo dotnetcloud setup");
            return 1;
        }

        ConsoleOutput.WriteSuccess($"Configuration saved to {CliConfiguration.GetConfigFilePath()}");

        // Write a one-time seed file with the admin password.
        // The server reads and deletes this file on first startup.
        // It is never persisted in config.json.
        var seedFilePath = Path.Combine(CliConfiguration.GetConfigDirectory(), ".admin-seed");
        try
        {
            File.WriteAllText(seedFilePath, adminPassword);

            // Restrict permissions on Linux so only root/service user can read it.
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(seedFilePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteWarning($"Could not write seed file: {ex.Message}");
            ConsoleOutput.WriteInfo("The admin user will need to be created manually.");
        }

        // Create directories
        try
        {
            Directory.CreateDirectory(config.DataDirectory);
            Directory.CreateDirectory(config.LogDirectory);
            Directory.CreateDirectory(config.BackupDirectory);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine();
            ConsoleOutput.WriteError("Permission denied creating data directories.");
            ConsoleOutput.WriteInfo("Re-run with sudo: sudo dotnetcloud setup");
            ConsoleOutput.WriteInfo("Your previous answers will be pre-filled.");
            return 1;
        }

        ConsoleOutput.WriteSuccess("Data directories created.");

        if (!EnsureTlsCertificateIfNeeded(config))
        {
            return 1;
        }

        // Harden the systemd service now that setup is complete.
        if (SystemdServiceHelper.ApplyHardening())
        {
            ConsoleOutput.WriteSuccess("Systemd service hardened (NoNewPrivileges, ProtectSystem, ProtectHome).");
        }

        // Start the service
        Console.WriteLine();
        var serviceStarted = false;
        if (SystemdServiceHelper.ServiceFileExists())
        {
            ConsoleOutput.WriteInfo("Starting DotNetCloud service...");
            if (SystemdServiceHelper.EnableAndStart())
            {
                serviceStarted = true;

                // Wait for health check
                ConsoleOutput.WriteInfo("Waiting for server to become healthy...");
                var healthy = await WaitForHealthAsync(config, timeoutSeconds: 30);
                if (healthy)
                {
                    ConsoleOutput.WriteSuccess("Server is healthy!");
                }
                else
                {
                    ConsoleOutput.WriteWarning("Server started but health check is not responding yet.");
                    ConsoleOutput.WriteInfo("It may still be running migrations. Check with:");
                    Console.WriteLine("    sudo systemctl status dotnetcloud");
                    Console.WriteLine("    sudo journalctl -u dotnetcloud -f");
                }
            }
            else
            {
                ConsoleOutput.WriteWarning("Could not start the service automatically.");
                ConsoleOutput.WriteInfo("Start manually: sudo systemctl start dotnetcloud");
                ConsoleOutput.WriteInfo("Check logs: sudo journalctl -u dotnetcloud -f");
            }
        }
        else
        {
            ConsoleOutput.WriteInfo("Start the server with: dotnetcloud start");
        }

        await SyncEnabledModulesToDatabaseAsync(config);

        // Show login URL
        var loginUrl = BuildLoginUrl(config);
        Console.WriteLine();
        ConsoleOutput.WriteHeader("Setup Complete!");
        ConsoleOutput.WriteInfo("Open your browser and log in at:");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"    {loginUrl}");
        Console.ResetColor();
        Console.WriteLine();
        ConsoleOutput.WriteInfo($"Login username: {config.AdminEmail}");
        ConsoleOutput.WriteInfo("Login password: the admin password you chose during this setup");
        Console.WriteLine();

        // Firewall & network guidance
        if (serviceStarted)
        {
            FirewallHelper.ShowNetworkGuidance(
                config.HttpPort,
                config.EnableHttps ? config.HttpsPort : null,
                config.EnableHttps);
        }

        return 0;
    }

    private static async Task SyncEnabledModulesToDatabaseAsync(CliConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            return;
        }

        try
        {
            await using var provider = ServiceProviderFactory.CreateFromConnectionString(config.ConnectionString);
            if (provider is null)
            {
                return;
            }

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DotNetCloud.Core.Data.Context.CoreDbContext>();

            var selectedModules = config.EnabledModules
                .Where(moduleId => !string.IsNullOrWhiteSpace(moduleId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var installedModules = await db.InstalledModules.ToListAsync();
            var installedById = installedModules.ToDictionary(m => m.ModuleId, StringComparer.OrdinalIgnoreCase);

            foreach (var moduleId in selectedModules)
            {
                if (!installedById.TryGetValue(moduleId, out var installed))
                {
                    db.InstalledModules.Add(new DotNetCloud.Core.Data.Entities.Modules.InstalledModule
                    {
                        ModuleId = moduleId,
                        Version = "1.0.0",
                        Status = "Enabled",
                        InstalledAt = DateTime.UtcNow,
                    });
                    continue;
                }

                if (!string.Equals(installed.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    installed.Status = "Enabled";
                }
            }

            foreach (var installed in installedModules)
            {
                if (!selectedModules.Contains(installed.ModuleId) &&
                    string.Equals(installed.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    installed.Status = "Disabled";
                }
            }

            await db.SaveChangesAsync();

            ConsoleOutput.WriteSuccess("Module selections synced to module registry.");
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteWarning($"Could not sync selected modules to the module registry: {ex.Message}");
            ConsoleOutput.WriteInfo("You can sync manually with: dotnetcloud module install <module-id>");
        }
    }

    /// <summary>
    /// Walks the user through individual database connection fields.
    /// </summary>
    private static string BuildConnectionStringGuided(string provider, out string? dbPassword)
    {
        dbPassword = null;

        switch (provider)
        {
            case "PostgreSQL":
            {
                var host = ConsoleOutput.Prompt("Database host", "localhost");
                var database = ConsoleOutput.Prompt("Database name", "dotnetcloud");
                var username = ConsoleOutput.Prompt("Database username", "dotnetcloud");

                Console.WriteLine();
                ConsoleOutput.WriteInfo("Choose a password for the database user.");
                ConsoleOutput.WriteInfo("Write it down — you'll need it if you ever reconfigure DotNetCloud.");

                while (true)
                {
                    var password = ConsoleOutput.PromptPassword("Database password");
                    var error = PasswordValidator.Validate(password);
                    if (error is not null)
                    {
                        ConsoleOutput.WriteError(error);
                        continue;
                    }

                    var confirm = ConsoleOutput.PromptPassword("Confirm database password");
                    if (password != confirm)
                    {
                        ConsoleOutput.WriteError("Passwords do not match. Please try again.");
                        continue;
                    }

                    dbPassword = password;
                    break;
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("  ║  IMPORTANT: Write down your database credentials!        ║");
                Console.WriteLine($"  ║  Username: {username,-46}║");
                Console.WriteLine("  ║  Password: (the password you just entered)               ║");
                Console.WriteLine("  ║                                                          ║");
                Console.WriteLine("  ║  These are NOT the same as your DotNetCloud login.       ║");
                Console.WriteLine("  ║  You will choose your DotNetCloud login next.            ║");
                Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine();

                return DatabaseSetupHelper.BuildPostgreSqlConnectionString(
                    host, database, username, dbPassword);
            }

            case "SqlServer":
            {
                var server = ConsoleOutput.Prompt("Server address", "localhost");
                var database = ConsoleOutput.Prompt("Database name", "dotnetcloud");
                var trusted = ConsoleOutput.PromptConfirm(
                    "Use Windows Authentication (Trusted Connection)?", defaultValue: true);

                if (trusted)
                {
                    return DatabaseSetupHelper.BuildSqlServerConnectionString(
                        server, database, null, null, trustedConnection: true);
                }

                var username = ConsoleOutput.Prompt("Database username", "dotnetcloud");

                while (true)
                {
                    var password = ConsoleOutput.PromptPassword("Database password");
                    var error = PasswordValidator.Validate(password);
                    if (error is not null)
                    {
                        ConsoleOutput.WriteError(error);
                        continue;
                    }

                    var confirm = ConsoleOutput.PromptPassword("Confirm database password");
                    if (password != confirm)
                    {
                        ConsoleOutput.WriteError("Passwords do not match. Please try again.");
                        continue;
                    }

                    dbPassword = password;
                    break;
                }

                return DatabaseSetupHelper.BuildSqlServerConnectionString(
                    server, database, username, dbPassword, trustedConnection: false);
            }

            case "MariaDB":
            {
                var server = ConsoleOutput.Prompt("Server address", "localhost");
                var database = ConsoleOutput.Prompt("Database name", "dotnetcloud");
                var username = ConsoleOutput.Prompt("Database username", "dotnetcloud");

                while (true)
                {
                    var password = ConsoleOutput.PromptPassword("Database password");
                    var error = PasswordValidator.Validate(password);
                    if (error is not null)
                    {
                        ConsoleOutput.WriteError(error);
                        continue;
                    }

                    var confirm = ConsoleOutput.PromptPassword("Confirm database password");
                    if (password != confirm)
                    {
                        ConsoleOutput.WriteError("Passwords do not match. Please try again.");
                        continue;
                    }

                    dbPassword = password;
                    break;
                }

                return DatabaseSetupHelper.BuildMariaDbConnectionString(
                    server, database, username, dbPassword);
            }

            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// Parses a semicolon-delimited connection string into key-value pairs.
    /// </summary>
    private static Dictionary<string, string> ParseConnectionString(string connectionString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kvp = part.Split('=', 2);
            if (kvp.Length == 2)
            {
                result[kvp[0].Trim()] = kvp[1].Trim();
            }
        }

        return result;
    }

    /// <summary>
    /// Polls the health endpoint until it responds or the timeout elapses.
    /// </summary>
    private static async Task<bool> WaitForHealthAsync(CliConfig config, int timeoutSeconds)
    {
        var healthUrl = $"http://localhost:{config.HttpPort}/health";

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await http.GetAsync(healthUrl);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Server not ready yet.
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        return false;
    }

    private static async Task<bool> VerifyDatabaseConnectionAsync(CliConfig config)
    {
        try
        {
            await using var provider = ServiceProviderFactory.CreateFromConnectionString(config.ConnectionString);
            if (provider is null)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "(not set)";
        }

        // Mask password values in connection strings
        var parts = connectionString.Split(';');
        for (var i = 0; i < parts.Length; i++)
        {
            var kvp = parts[i].Split('=', 2);
            if (kvp.Length == 2)
            {
                var key = kvp[0].Trim().ToUpperInvariant();
                if (key is "PASSWORD" or "PWD")
                {
                    parts[i] = $"{kvp[0]}=****";
                }
            }
        }

        return string.Join(';', parts);
    }

    private static string BuildLoginUrl(CliConfig config)
    {
        // If Let's Encrypt is configured, the user has a real domain.
        if (config.EnableHttps && config.UseLetsEncrypt
            && !string.IsNullOrWhiteSpace(config.LetsEncryptDomain))
        {
            // Port 443 is the default for HTTPS — omit it from the URL.
            return config.HttpsPort == 443
                ? $"https://{config.LetsEncryptDomain}"
                : $"https://{config.LetsEncryptDomain}:{config.HttpsPort}";
        }

        if (config.EnableHttps && config.UseSelfSignedTls
            && !string.IsNullOrWhiteSpace(config.SelfSignedTlsHost))
        {
            return $"https://{config.SelfSignedTlsHost}:{config.HttpsPort}";
        }

        if (config.EnableHttps)
        {
            return $"https://localhost:{config.HttpsPort}";
        }

        return $"http://localhost:{config.HttpPort}";
    }

    private static bool EnsureTlsCertificateIfNeeded(CliConfig config)
    {
        if (!config.EnableHttps || !config.UseSelfSignedTls)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(config.TlsCertificatePath))
        {
            ConsoleOutput.WriteError("Self-signed TLS mode selected, but certificate path is empty.");
            return false;
        }

        try
        {
            var certPath = config.TlsCertificatePath;
            var certDir = Path.GetDirectoryName(certPath);
            if (!string.IsNullOrWhiteSpace(certDir))
            {
                Directory.CreateDirectory(certDir);
            }

            var host = string.IsNullOrWhiteSpace(config.SelfSignedTlsHost)
                ? "localhost"
                : config.SelfSignedTlsHost.Trim();

            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                $"CN={host}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    false));
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(host);
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
            request.CertificateExtensions.Add(sanBuilder.Build());

            using var cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddYears(1));

            var pfxBytes = cert.Export(X509ContentType.Pfx, string.Empty);
            File.WriteAllBytes(certPath, pfxBytes);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(certPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            ConsoleOutput.WriteSuccess($"Generated self-signed TLS certificate: {certPath}");
            ConsoleOutput.WriteInfo("Browsers/devices will show a trust warning unless this certificate is explicitly trusted.");
            return true;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Failed to generate self-signed TLS certificate: {ex.Message}");
            ConsoleOutput.WriteInfo("You can switch to an existing certificate path or rerun setup.");
            return false;
        }
    }
}
