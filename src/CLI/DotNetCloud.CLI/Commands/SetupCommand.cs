using System.CommandLine;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Interactive first-run wizard: database selection, connection string,
/// admin user creation, organization setup, TLS configuration, and module selection.
/// </summary>
internal static class SetupCommand
{
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

        if (CliConfiguration.ConfigExists())
        {
            ConsoleOutput.WriteWarning("A configuration already exists.");
            if (!ConsoleOutput.PromptConfirm("Overwrite existing configuration?"))
            {
                ConsoleOutput.WriteInfo("Setup cancelled.");
                return 0;
            }
        }

        var config = new CliConfig();

        // Step 1: Database selection
        ConsoleOutput.WriteStep(1, 9, "Database Configuration");
        var dbChoice = ConsoleOutput.PromptChoice(
            "Select database provider:",
            ["PostgreSQL (recommended)", "SQL Server", "MariaDB"]);

        config.DatabaseProvider = dbChoice switch
        {
            0 => "PostgreSQL",
            1 => "SqlServer",
            2 => "MariaDB",
            _ => "PostgreSQL"
        };

        var defaultConnStr = config.DatabaseProvider switch
        {
            "PostgreSQL" => "Host=localhost;Database=dotnetcloud;Username=postgres;Password=postgres",
            "SqlServer" => "Server=localhost;Database=dotnetcloud;Trusted_Connection=True;TrustServerCertificate=True",
            "MariaDB" => "Server=localhost;Database=dotnetcloud;User=root;Password=root",
            _ => ""
        };

        config.ConnectionString = ConsoleOutput.Prompt("Connection string", defaultConnStr);

        // Step 2: Verify database connection
        ConsoleOutput.WriteStep(2, 9, "Verifying Database Connection");
        var canConnect = await VerifyDatabaseConnectionAsync(config);
        if (!canConnect)
        {
            ConsoleOutput.WriteError("Could not connect to the database.");
            if (!ConsoleOutput.PromptConfirm("Continue anyway?"))
            {
                return 1;
            }
        }
        else
        {
            ConsoleOutput.WriteSuccess("Database connection verified.");
        }

        // Step 3: Admin user
        ConsoleOutput.WriteStep(3, 9, "Admin User Configuration");
        config.AdminEmail = ConsoleOutput.Prompt("Admin email address");
        var adminPassword = ConsoleOutput.PromptPassword("Admin password");
        var confirmPassword = ConsoleOutput.PromptPassword("Confirm admin password");

        if (adminPassword != confirmPassword)
        {
            ConsoleOutput.WriteError("Passwords do not match.");
            return 1;
        }

        // Step 4: MFA setup prompt
        ConsoleOutput.WriteStep(4, 9, "Multi-Factor Authentication");
        var enableMfa = ConsoleOutput.PromptConfirm("Enable TOTP MFA for admin account?", defaultValue: true);
        if (enableMfa)
        {
            ConsoleOutput.WriteInfo("MFA will be configured on first login via the web UI.");
        }

        // Step 5: Organization
        ConsoleOutput.WriteStep(5, 9, "Organization Setup");
        config.OrganizationName = ConsoleOutput.Prompt("Organization name", "My Organization");

        // Step 6: TLS/HTTPS
        ConsoleOutput.WriteStep(6, 9, "TLS/HTTPS Configuration");
        config.EnableHttps = ConsoleOutput.PromptConfirm("Enable HTTPS?", defaultValue: true);

        if (config.EnableHttps)
        {
            config.HttpsPort = int.TryParse(
                ConsoleOutput.Prompt("HTTPS port", "5443"), out var httpsPort) ? httpsPort : 5443;

            config.UseLetsEncrypt = ConsoleOutput.PromptConfirm("Use Let's Encrypt for automatic certificates?");
            if (config.UseLetsEncrypt)
            {
                config.LetsEncryptDomain = ConsoleOutput.Prompt("Domain name (e.g., cloud.example.com)");
            }
            else
            {
                config.TlsCertificatePath = ConsoleOutput.Prompt("Path to TLS certificate (PFX or PEM)", "");
            }
        }

        config.HttpPort = int.TryParse(
            ConsoleOutput.Prompt("HTTP port", "5080"), out var httpPort) ? httpPort : 5080;

        // Step 7: Module selection
        ConsoleOutput.WriteStep(7, 9, "Module Selection");
        ConsoleOutput.WriteInfo("Select modules to enable (more can be enabled later):");
        var availableModules = new[]
        {
            "dotnetcloud.files",
            "dotnetcloud.chat",
            "dotnetcloud.contacts",
            "dotnetcloud.calendar",
            "dotnetcloud.notes",
            "dotnetcloud.deck"
        };

        foreach (var moduleId in availableModules)
        {
            if (ConsoleOutput.PromptConfirm($"  Enable {moduleId}?", defaultValue: moduleId is "dotnetcloud.files" or "dotnetcloud.chat"))
            {
                config.EnabledModules.Add(moduleId);
            }
        }

        // Step 8: Data directories
        ConsoleOutput.WriteStep(8, 9, "Data Directories");
        config.DataDirectory = ConsoleOutput.Prompt("Data directory", config.DataDirectory);
        config.LogDirectory = ConsoleOutput.Prompt("Log directory", config.LogDirectory);
        config.BackupDirectory = ConsoleOutput.Prompt("Backup directory", config.BackupDirectory);

        // Step 9: Collabora Online / document editing
        ConsoleOutput.WriteStep(9, 9, "Collabora Online (Document Editing)");
        ConsoleOutput.WriteInfo("Collabora Online enables in-browser editing of Word, Excel, and PowerPoint files.");

        if (config.EnabledModules.Contains("dotnetcloud.files"))
        {
            var enableCollabora = ConsoleOutput.PromptConfirm("Enable Collabora Online document editing?", defaultValue: false);

            if (enableCollabora)
            {
                var collaboraChoice = ConsoleOutput.PromptChoice(
                    "Collabora Online installation:",
                    [
                        "Install built-in Collabora CODE automatically",
                        "Connect to an existing Collabora Online server"
                    ]);

                if (collaboraChoice == 0)
                {
                    config.CollaboraMode = "BuiltIn";
                    ConsoleOutput.WriteSuccess("Collabora CODE will be installed automatically.");
                }
                else
                {
                    config.CollaboraMode = "External";
                    config.CollaboraUrl = ConsoleOutput.Prompt(
                        "Collabora Online server URL",
                        "https://collabora.example.com");
                }
            }
            else
            {
                config.CollaboraMode = "None";
            }
        }
        else
        {
            ConsoleOutput.WriteInfo("Skipped (dotnetcloud.files module not enabled).");
        }

        // Summary
        Console.WriteLine();
        ConsoleOutput.WriteHeader("Configuration Summary");
        ConsoleOutput.WriteDetail("Database", config.DatabaseProvider);
        ConsoleOutput.WriteDetail("Connection", MaskConnectionString(config.ConnectionString));
        ConsoleOutput.WriteDetail("Admin Email", config.AdminEmail ?? "(not set)");
        ConsoleOutput.WriteDetail("Organization", config.OrganizationName ?? "(not set)");
        ConsoleOutput.WriteDetail("HTTPS", config.EnableHttps ? $"Enabled (port {config.HttpsPort})" : "Disabled");
        ConsoleOutput.WriteDetail("HTTP Port", config.HttpPort.ToString());
        ConsoleOutput.WriteDetail("Modules", config.EnabledModules.Count > 0
            ? string.Join(", ", config.EnabledModules) : "(none)");
        ConsoleOutput.WriteDetail("Data Dir", config.DataDirectory);
        ConsoleOutput.WriteDetail("Log Dir", config.LogDirectory);
        ConsoleOutput.WriteDetail("Backup Dir", config.BackupDirectory);
        ConsoleOutput.WriteDetail("Collabora", config.CollaboraMode switch
        {
            "BuiltIn" => "Built-in CODE (installed via APT)",
            "External" => $"External ({config.CollaboraUrl})",
            _ => "Disabled"
        });

        Console.WriteLine();
        if (!ConsoleOutput.PromptConfirm("Save this configuration?", defaultValue: true))
        {
            ConsoleOutput.WriteInfo("Setup cancelled.");
            return 0;
        }

        // Save
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
            ConsoleOutput.WriteError(
                "Permission denied creating data directories.");
            ConsoleOutput.WriteInfo("Re-run with sudo: sudo dotnetcloud setup");
            return 1;
        }

        ConsoleOutput.WriteSuccess("Data directories created.");

        Console.WriteLine();
        ConsoleOutput.WriteInfo("Setup complete! Start the server with: dotnetcloud serve");

        return 0;
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
}
