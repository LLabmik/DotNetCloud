using System.CommandLine;
using System.Diagnostics;
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
    private const string DefaultPostgreSqlDatabase = "dotnetcloud";
    private const string DefaultPostgreSqlUser = "dotnetcloud";
    private const string ReverseProxyBeginnerGuideUrl = "https://github.com/LLabmik/DotNetCloud/blob/main/docs/admin/server/REVERSE_PROXY_BEGINNER_GUIDE.md";

    /// <summary>
    /// Creates the <c>setup</c> command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("setup", "Interactive first-run setup wizard");
        var beginnerOption = new Option<bool>("--beginner")
        {
            Description = "Use the recommended beginner-friendly setup for a local/home server install"
        };

        command.Options.Add(beginnerOption);
        command.SetAction(parseResult =>
        {
            var beginnerMode = parseResult.GetValue(beginnerOption);
            return RunSetupWizardAsync(beginnerMode);
        });
        return command;
    }

    private static async Task<int> RunSetupWizardAsync(bool beginnerMode)
    {
        ConsoleOutput.WriteHeader("DotNetCloud Setup Wizard");

        if (beginnerMode)
        {
            ConsoleOutput.WriteInfo("Beginner mode enabled: DotNetCloud will use the recommended local/home-server setup.");
            ConsoleOutput.WriteInfo("This keeps the questions short and applies safe defaults you can change later.");
            Console.WriteLine();
        }

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
        string? dbPassword = null;

        if (beginnerMode)
        {
            config.DatabaseProvider = "PostgreSQL";
            dbPassword = GetOrCreateBeginnerDatabasePassword(config.ConnectionString);
            config.ConnectionString = DatabaseSetupHelper.BuildPostgreSqlConnectionString(
                "localhost",
                DefaultPostgreSqlDatabase,
                DefaultPostgreSqlUser,
                dbPassword);

            ConsoleOutput.WriteInfo("Using local PostgreSQL with automatic setup.");
            ConsoleOutput.WriteDetail("Database", DefaultPostgreSqlDatabase);
            ConsoleOutput.WriteDetail("Database User", DefaultPostgreSqlUser);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && !DatabaseSetupHelper.IsPostgreSqlInstalled())
            {
                Console.WriteLine();
                ConsoleOutput.WriteWarning("PostgreSQL is not installed on this system.");
                if (!ConsoleOutput.PromptConfirm("Install PostgreSQL automatically now?", defaultValue: true))
                {
                    ConsoleOutput.WriteError("Beginner mode needs a local PostgreSQL installation.");
                    ConsoleOutput.WriteInfo("Run setup again without --beginner if you want to use another database server.");
                    return 1;
                }

                if (!DatabaseSetupHelper.InstallPostgreSql())
                {
                    ConsoleOutput.WriteError("PostgreSQL installation failed.");
                    ConsoleOutput.WriteInfo("Install manually: sudo apt-get install postgresql");
                    return 1;
                }
            }
        }
        else
        {
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

            var providerChanged = previousDbIndex != dbChoice;
            var hasExistingConnStr = !string.IsNullOrWhiteSpace(config.ConnectionString) && !providerChanged;

            if (hasExistingConnStr)
            {
                config.ConnectionString = ConsoleOutput.Prompt("Connection string", config.ConnectionString);
            }
            else
            {
                var inputMode = ConsoleOutput.PromptChoice(
                    "How would you like to configure the database connection?",
                    ["Guided (answer a few simple questions)", "Advanced (enter full connection string)"],
                    defaultIndex: 0);

                if (inputMode == 1)
                {
                    var defaultConnStr = config.DatabaseProvider switch
                    {
                        "PostgreSQL" => "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=yourpassword",
                        "SqlServer" => "Server=localhost;Database=dotnetcloud;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
                        "MariaDB" => "Server=localhost;Database=dotnetcloud;User=dotnetcloud;Password=yourpassword",
                        _ => ""
                    };
                    config.ConnectionString = ConsoleOutput.Prompt("Connection string", defaultConnStr);
                }
                else
                {
                    config.ConnectionString = BuildConnectionStringGuided(config.DatabaseProvider, out dbPassword);
                }
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
                ConsoleOutput.WriteInfo(beginnerMode
                    ? "Setting up the local DotNetCloud database automatically."
                    : "The database or user may not exist yet.");

                if (beginnerMode || ConsoleOutput.PromptConfirm("Create the database and user now?", defaultValue: true))
                {
                    var parts = ParseConnectionString(config.ConnectionString);
                    var dbName = parts.GetValueOrDefault("database", DefaultPostgreSqlDatabase);
                    var dbUser = parts.GetValueOrDefault("username", DefaultPostgreSqlUser);

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

                        if (beginnerMode)
                        {
                            return 1;
                        }
                    }
                }
            }

            if (!canConnect && beginnerMode)
            {
                ConsoleOutput.WriteError("DotNetCloud could not finish the recommended local database setup.");
                ConsoleOutput.WriteInfo("Fix PostgreSQL, then re-run: sudo dotnetcloud setup --beginner");
                return 1;
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

        if (beginnerMode)
        {
            var deploymentChoice = ConsoleOutput.PromptChoice(
                "How will people access DotNetCloud?",
                [
                    "Private home/LAN or local test install (recommended)",
                    "Public internet with a real domain name behind a reverse proxy (recommended)",
                    "Public internet directly on this DotNetCloud server"
                ],
                defaultIndex: GetDefaultBeginnerDeploymentChoice(config));

            if (deploymentChoice == 0)
            {
                config.EnableHttps = true;
                config.HttpsPort = config.HttpsPort > 0 ? config.HttpsPort : 5443;
                config.HttpPort = config.HttpPort > 0 ? config.HttpPort : 5080;
                config.UseLetsEncrypt = false;
                config.UseSelfSignedTls = true;
                config.LetsEncryptDomain = null;
                config.SelfSignedTlsHost = GetDefaultBeginnerTlsHost(config);
                config.TlsCertificatePath = GetDefaultSelfSignedCertificatePath(config, config.TlsCertificatePath);

                ConsoleOutput.WriteInfo("Using HTTPS with a self-signed certificate for a private/home-server install.");
                ConsoleOutput.WriteDetail("HTTPS URL", $"https://{config.SelfSignedTlsHost}:{config.HttpsPort}");
                ConsoleOutput.WriteDetail("Fallback HTTP URL", $"http://localhost:{config.HttpPort}");
                ConsoleOutput.WriteInfo("You can switch to a public-domain setup later if you publish DotNetCloud on the internet.");
            }
            else if (deploymentChoice == 1)
            {
                config.EnableHttps = false;
                config.HttpPort = config.HttpPort > 0 ? config.HttpPort : 5080;
                config.UseLetsEncrypt = false;
                config.UseSelfSignedTls = false;
                config.SelfSignedTlsHost = null;
                config.TlsCertificatePath = null;
                config.LetsEncryptDomain = ConsoleOutput.Prompt(
                    "Public domain name (for example cloud.example.com)",
                    config.LetsEncryptDomain);

                ConsoleOutput.WriteInfo("Using reverse-proxy mode for a public-domain install.");
                ConsoleOutput.WriteDetail("Public URL", $"https://{config.LetsEncryptDomain}");
                ConsoleOutput.WriteDetail("Internal DotNetCloud URL", $"http://localhost:{config.HttpPort}");
                ConsoleOutput.WriteInfo("DotNetCloud will run locally over HTTP so you can place nginx, Apache, Caddy, or another reverse proxy in front of it.");
                ConsoleOutput.WriteInfo("TLS for the public domain should be handled by the reverse proxy.");
                ConsoleOutput.WriteInfo("This is recommended for most public installs because the reverse proxy can manage ports 80/443, TLS renewal, and headers for you.");
            }
            else
            {
                config.EnableHttps = true;
                config.HttpsPort = config.HttpsPort > 0 ? config.HttpsPort : 5443;
                config.HttpPort = config.HttpPort > 0 ? config.HttpPort : 5080;
                config.UseLetsEncrypt = false;
                config.UseSelfSignedTls = false;
                config.LetsEncryptDomain = ConsoleOutput.Prompt(
                    "Public domain name (for example cloud.example.com)",
                    config.LetsEncryptDomain);
                config.TlsCertificatePath = ConsoleOutput.Prompt(
                    "Path to your public TLS certificate file (PFX)",
                    config.TlsCertificatePath ?? "");

                ConsoleOutput.WriteInfo("Using direct public mode on the DotNetCloud server itself.");
                ConsoleOutput.WriteDetail("Public URL", $"https://{config.LetsEncryptDomain}:{config.HttpsPort}");
                ConsoleOutput.WriteDetail("TLS Certificate", config.TlsCertificatePath);
                ConsoleOutput.WriteInfo("Use this only if you already have a certificate file for the domain and want DotNetCloud to serve HTTPS directly.");
                ConsoleOutput.WriteInfo("A reverse proxy is still recommended for most public installs because it simplifies ports 80/443, certificate renewal, and future services on the same machine.");
            }
        }
        else
        {
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

                    config.TlsCertificatePath = GetDefaultSelfSignedCertificatePath(config, config.TlsCertificatePath);
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
        }

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
            "dotnetcloud.tracks"
        };

        var previouslyEnabled = config.EnabledModules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        config.EnabledModules.Clear();

        foreach (var moduleId in requiredModules)
        {
            config.EnabledModules.Add(moduleId);
        }

        ConsoleOutput.WriteInfo("Required modules (always enabled): dotnetcloud.files, dotnetcloud.chat");

        if (beginnerMode)
        {
            ConsoleOutput.WriteInfo("Keeping the first install simple: only the required modules are enabled.");
            ConsoleOutput.WriteInfo("You can enable Contacts, Calendar, Notes, and Tracks later from the admin UI.");
        }
        else if (optionalModules.Length > 0)
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

        var customizeDirs = beginnerMode
            ? false
            : ConsoleOutput.PromptConfirm("Customize data directories?");
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
            if (beginnerMode)
            {
                config.CollaboraMode = "None";
                ConsoleOutput.WriteInfo("Skipping document editing on the first install to keep setup simple.");
                ConsoleOutput.WriteInfo("You can enable it later with: sudo dotnetcloud collabora-install");
            }
            else
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
                            "Install built-in Collabora CODE (free, ~20 concurrent editors)",
                            "Connect to an external Collabora Online server (paid/unlimited users)"
                        ],
                        defaultIndex: previousCollaboraIndex);

                    if (collaboraChoice == 0)
                    {
                        config.CollaboraMode = "BuiltIn";
                        ConsoleOutput.WriteSuccess("Collabora CODE will be installed automatically.");
                        ConsoleOutput.WriteInfo("CODE supports ~10-20 concurrent editors. Need more? Upgrade to a");
                        ConsoleOutput.WriteInfo("paid Collabora Online license and switch to External mode later.");
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
        if (beginnerMode)
        {
            ConsoleOutput.WriteDetail("Setup Mode", "Beginner (recommended local/home-server install)");
        }
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
        config.ConfigSchemaVersion = CliConfiguration.CurrentConfigSchemaVersion;

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

        if (beginnerMode)
        {
            WriteBeginnerCompletionSummary(config, loginUrl, serviceStarted);
        }

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
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;

        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var healthUrls = GetLocalHealthProbeUrls(config);

        while (DateTime.UtcNow < deadline)
        {
            foreach (var healthUrl in healthUrls)
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
                    // Server not ready on this endpoint yet.
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        return false;
    }

    private static List<string> GetLocalHealthProbeUrls(CliConfig config)
    {
        var urls = new List<string>();

        if (config.EnableHttps)
        {
            urls.Add($"https://localhost:{config.HttpsPort}/health");
        }

        urls.Add($"http://localhost:{config.HttpPort}/health");

        return urls;
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

    private static void WriteBeginnerCompletionSummary(CliConfig config, string loginUrl, bool serviceStarted)
    {
        ConsoleOutput.WriteHeader("Beginner Install Summary");
        ConsoleOutput.WriteInfo("DotNetCloud used these settings for your first install:");
        ConsoleOutput.WriteDetail("Database", "Local PostgreSQL on this machine");
        ConsoleOutput.WriteDetail("Database Name", DefaultPostgreSqlDatabase);
        ConsoleOutput.WriteDetail("Database User", DefaultPostgreSqlUser);
        ConsoleOutput.WriteDetail("Install Type", GetBeginnerInstallType(config));
        ConsoleOutput.WriteDetail("HTTPS", config.EnableHttps ? $"Enabled on port {config.HttpsPort}" : "Handled by your reverse proxy");
        ConsoleOutput.WriteDetail("Certificate", config.UseSelfSignedTls
            ? $"Self-signed for {config.SelfSignedTlsHost}"
            : IsBeginnerPublicReverseProxyMode(config) ? "Managed by your reverse proxy" : config.UseLetsEncrypt ? "Let's Encrypt" : "Existing certificate");
        ConsoleOutput.WriteDetail("Login URL", IsBeginnerPublicReverseProxyMode(config)
            ? $"https://{config.LetsEncryptDomain} (after reverse proxy setup)"
            : loginUrl);
        if (IsBeginnerPublicReverseProxyMode(config))
        {
            ConsoleOutput.WriteDetail("Internal App URL", loginUrl);
        }
        ConsoleOutput.WriteDetail("Required Modules", string.Join(", ", config.EnabledModules));
        ConsoleOutput.WriteDetail("Document Editing", "Skipped for the first install");
        ConsoleOutput.WriteDetail("Data Directory", config.DataDirectory);
        ConsoleOutput.WriteDetail("Log Directory", config.LogDirectory);
        ConsoleOutput.WriteDetail("Backup Directory", config.BackupDirectory);
        Console.WriteLine();

        ConsoleOutput.WriteInfo("What you need to know:");
        if (IsBeginnerPublicReverseProxyMode(config))
        {
            Console.WriteLine($"    1. DotNetCloud is running locally at {loginUrl}.");
            Console.WriteLine($"    2. Set up your reverse proxy to forward https://{config.LetsEncryptDomain} to http://localhost:{config.HttpPort}.");
            Console.WriteLine($"    3. Add TLS for {config.LetsEncryptDomain} on the reverse proxy.");
            Console.WriteLine($"    4. After that is done, open https://{config.LetsEncryptDomain} and sign in with {config.AdminEmail}.");
            Console.WriteLine("    5. If you want to test locally before the reverse proxy is ready, use the internal app URL shown above.");
            Console.WriteLine("       A reverse proxy is recommended for public installs because it keeps DotNetCloud on a local-only port and makes TLS/443 handling easier.");
            Console.WriteLine($"       Beginner guide: {ReverseProxyBeginnerGuideUrl}");
        }
        else if (IsBeginnerPublicDirectMode(config))
        {
            Console.WriteLine($"    1. DotNetCloud is configured to serve your public domain directly at {loginUrl}.");
            Console.WriteLine($"    2. Make sure your TLS certificate file exists at {config.TlsCertificatePath}.");
            Console.WriteLine($"    3. Make sure your firewall/router sends public traffic for {config.LetsEncryptDomain} to port {config.HttpsPort} on this server.");
            Console.WriteLine($"    4. Open {loginUrl} and sign in with {config.AdminEmail}.");
            Console.WriteLine("    5. A reverse proxy is still recommended for most public installs because it makes ports 80/443, certificate renewal, and future services easier to manage.");
            Console.WriteLine($"       If you want to switch to a reverse proxy later, use this guide: {ReverseProxyBeginnerGuideUrl}");
        }
        else
        {
            Console.WriteLine($"    1. Open {loginUrl} in your browser.");
            Console.WriteLine($"    2. Sign in with {config.AdminEmail} and the admin password you chose.");
            if (config.UseSelfSignedTls)
            {
                Console.WriteLine("    3. Your browser will likely show a certificate warning the first time because this install uses a self-signed certificate.");
                Console.WriteLine("       That is expected on a private/home server install.");
            }
            else
            {
                Console.WriteLine("    3. HTTPS is enabled and ready to use.");
            }
        }

        if (serviceStarted)
        {
            Console.WriteLine((IsBeginnerPublicReverseProxyMode(config) || IsBeginnerPublicDirectMode(config))
                ? "    6. The DotNetCloud service is already running."
                : "    4. The DotNetCloud service is already running.");
        }
        else
        {
            Console.WriteLine((IsBeginnerPublicReverseProxyMode(config) || IsBeginnerPublicDirectMode(config))
                ? "    6. The service did not confirm as healthy yet. Re-run: sudo dotnetcloud setup --beginner"
                : "    4. The service did not confirm as healthy yet. Re-run: sudo dotnetcloud setup --beginner");
        }

        Console.WriteLine((IsBeginnerPublicReverseProxyMode(config) || IsBeginnerPublicDirectMode(config))
            ? "    7. Later, if you want more features, you can run setup again or install document editing with: sudo dotnetcloud collabora-install"
            : "    5. Later, if you want more features, you can run setup again or install document editing with: sudo dotnetcloud collabora-install");
        Console.WriteLine();
    }

    private static int GetDefaultBeginnerDeploymentChoice(CliConfig config)
    {
        if (IsBeginnerPublicDirectMode(config))
        {
            return 2;
        }

        return IsBeginnerPublicReverseProxyMode(config) ? 1 : 0;
    }

    private static bool IsBeginnerPublicReverseProxyMode(CliConfig config)
    {
        return !config.EnableHttps
            && !string.IsNullOrWhiteSpace(config.LetsEncryptDomain)
            && !config.UseSelfSignedTls;
    }

    private static bool IsBeginnerPublicDirectMode(CliConfig config)
    {
        return config.EnableHttps
            && !config.UseSelfSignedTls
            && !string.IsNullOrWhiteSpace(config.LetsEncryptDomain)
            && !string.IsNullOrWhiteSpace(config.TlsCertificatePath);
    }

    private static string GetBeginnerInstallType(CliConfig config)
    {
        if (IsBeginnerPublicReverseProxyMode(config))
        {
            return "Public domain behind a reverse proxy";
        }

        if (IsBeginnerPublicDirectMode(config))
        {
            return "Public domain served directly by DotNetCloud";
        }

        return "Private home/LAN or local test install";
    }

    private static string GetOrCreateBeginnerDatabasePassword(string? existingConnectionString)
    {
        var parts = ParseConnectionString(existingConnectionString ?? string.Empty);
        var existingPassword = parts.GetValueOrDefault("password")
            ?? parts.GetValueOrDefault("pwd");

        if (!string.IsNullOrWhiteSpace(existingPassword))
        {
            return existingPassword;
        }

        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', 'A')
            .Replace('/', 'b');
    }

    private static string GetDefaultBeginnerTlsHost(CliConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.SelfSignedTlsHost))
        {
            return config.SelfSignedTlsHost;
        }

        try
        {
            var hostName = System.Net.Dns.GetHostName();
            var fqdn = System.Net.Dns.GetHostEntry(hostName).HostName;
            if (!string.IsNullOrWhiteSpace(fqdn))
            {
                return fqdn;
            }
        }
        catch
        {
            // Fall back to the local machine name below.
        }

        return Environment.MachineName;
    }

    private static string GetDefaultSelfSignedCertificatePath(CliConfig config, string? currentPath)
    {
        var certDir = CliConfiguration.IsSystemInstall
            ? Path.Combine(CliConfiguration.GetConfigDirectory(), "certs")
            : Path.Combine(config.DataDirectory, "certs");

        var shouldNormalizePath = string.IsNullOrWhiteSpace(currentPath)
            || !Path.IsPathRooted(currentPath)
            || string.Equals(
                Path.GetFileName(currentPath),
                "dotnetcloud-localhost.pfx",
                StringComparison.OrdinalIgnoreCase);

        return shouldNormalizePath
            ? Path.Combine(certDir, "dotnetcloud-selfsigned.pfx")
            : currentPath!;
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

            if (File.Exists(certPath))
            {
                if (!EnsureCertificateFilePermissions(certPath))
                {
                    return false;
                }

                ConsoleOutput.WriteSuccess($"Using existing self-signed TLS certificate: {certPath}");
                return true;
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

            if (!EnsureCertificateFilePermissions(certPath))
            {
                return false;
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

    private static bool EnsureCertificateFilePermissions(string certPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            // Linux system installs run the service as dotnetcloud:dotnetcloud.
            // Keep cert private while still readable by the service user via group read.
            if (CliConfiguration.IsSystemInstall)
            {
                var ownerUpdated = TryRunCommand("chown", "root:dotnetcloud", certPath);
                if (!ownerUpdated)
                {
                    ConsoleOutput.WriteError("Failed to set TLS certificate ownership to root:dotnetcloud.");
                    ConsoleOutput.WriteInfo("Ensure cert file is readable by the dotnetcloud service user.");
                    return false;
                }

                File.SetUnixFileMode(certPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            }
            else
            {
                File.SetUnixFileMode(certPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            return true;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Failed to set TLS certificate permissions: {ex.Message}");
            return false;
        }
    }

    private static bool TryRunCommand(string fileName, params string[] arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
