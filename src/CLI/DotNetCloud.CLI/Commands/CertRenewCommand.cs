using System.CommandLine;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// CLI command to renew an existing Let's Encrypt certificate.
/// Can be run manually or triggered by systemd timers / cron for auto-renewal.
/// </summary>
internal static class CertRenewCommand
{
    /// <summary>
    /// Creates the <c>cert-renew</c> command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("cert-renew", "Renew Let's Encrypt TLS certificate if expiring within 30 days");

        command.SetAction(async _ =>
        {
            if (!CliConfiguration.TryLoad(out var config, out var error))
            {
                ConsoleOutput.WriteError(error ?? "Could not load configuration.");
                return 1;
            }

            if (!config.EnableHttps || !config.UseLetsEncrypt)
            {
                ConsoleOutput.WriteInfo("Let's Encrypt is not configured for this installation.");
                ConsoleOutput.WriteInfo("Run 'dotnetcloud setup' and select Let's Encrypt as your TLS mode.");
                return 0;
            }

            var domain = config.LetsEncryptDomain;
            if (string.IsNullOrWhiteSpace(domain))
            {
                ConsoleOutput.WriteError("Let's Encrypt is enabled but no domain is configured.");
                ConsoleOutput.WriteInfo("Run 'dotnetcloud setup' to configure the domain.");
                return 1;
            }

            var adminEmail = config.AdminEmail;
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                adminEmail = $"admin@{domain}";
            }

            var certPath = AcmeService.GetCertificatePath(config, domain);

            // Check if renewal is needed
            if (File.Exists(certPath) && !AcmeService.IsCertificateExpiringSoon(certPath))
            {
                ConsoleOutput.WriteInfo($"Certificate for {domain} is still valid. No renewal needed.");

                using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(
                    System.IO.File.ReadAllBytes(certPath));
                var daysRemaining = (cert.NotAfter - DateTime.UtcNow).Days;
                ConsoleOutput.WriteDetail("Expires", cert.NotAfter.ToString("yyyy-MM-dd"));
                ConsoleOutput.WriteDetail("Days remaining", daysRemaining.ToString());
                return 0;
            }

            ConsoleOutput.WriteInfo($"Renewing Let's Encrypt certificate for {domain}...");

            // Check port 80 availability
            if (!AcmeService.IsPort80Available())
            {
                ConsoleOutput.WriteError("Port 80 is not available. Let's Encrypt HTTP-01 validation requires port 80.");
                ConsoleOutput.WriteInfo("Stop any service using port 80 and try again.");
                return 1;
            }

            var success = await AcmeService.ProvisionCertificateAsync(
                domain,
                adminEmail,
                config);

            if (success)
            {
                ConsoleOutput.WriteSuccess($"Let's Encrypt certificate for {domain} renewed.");
                ConsoleOutput.WriteDetail("Certificate path", certPath);

                // Sync updated cert and env file to Collabora Online (coolwsd) if running in BuiltIn mode
                if (string.Equals(config.CollaboraMode, "BuiltIn", StringComparison.OrdinalIgnoreCase))
                {
                    SyncCollaboraCerts(config, domain);
                    WriteEnvironmentFile(config);
                }

                // Restart the service to pick up the new certificate
                if (SystemdServiceHelper.ServiceFileExists())
                {
                    ConsoleOutput.WriteInfo("Restarting DotNetCloud service to pick up new certificate...");
                    if (SystemdServiceHelper.RestartService())
                    {
                        ConsoleOutput.WriteSuccess("Service restarted.");
                    }
                    else
                    {
                        ConsoleOutput.WriteWarning("Could not restart service automatically.");
                        ConsoleOutput.WriteInfo("Restart manually: sudo systemctl restart dotnetcloud");
                    }
                }

                return 0;
            }
            else
            {
                ConsoleOutput.WriteError("Certificate renewal failed.");
                return 1;
            }
        });

        return command;
    }

    /// <summary>
    /// Syncs the renewed Let's Encrypt certificate to Collabora Online (coolwsd).
    /// Extracts the private key, server cert, and CA chain from the PEM bundle
    /// and writes them to /etc/coolwsd/, then restarts coolwsd.
    /// </summary>
    internal static void SyncCollaboraCerts(CliConfig config, string domain)
    {
        var pemPath = System.IO.Path.ChangeExtension(
            AcmeService.GetCertificatePath(config, domain), ".pem");
        ProvisionCollaboraCertsFromPem(pemPath);
    }

    /// <summary>
    /// Provisions Collabora Online certs from the configured TLS certificate.
    /// Works for both Let's Encrypt and self-signed setups. Called during initial
    /// setup and on cert renewal.
    /// </summary>
    internal static void ProvisionCollaboraCertsFromConfig(CliConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.TlsCertificatePath))
        {
            ConsoleOutput.WriteWarning("No TLS certificate configured. Skipping Collabora cert sync.");
            return;
        }

        var pemPath = System.IO.Path.ChangeExtension(config.TlsCertificatePath, ".pem");
        if (!File.Exists(pemPath))
        {
            ConsoleOutput.WriteWarning($"PEM bundle not found at {pemPath}. Skipping Collabora cert sync.");
            return;
        }

        ProvisionCollaboraCertsFromPem(pemPath);
    }

    /// <summary>
    /// Writes the runtime environment file consumed by the systemd unit's EnvironmentFile directive.
    /// Generates /etc/dotnetcloud/env with Collabora, admin credentials, and other runtime variables.
    /// </summary>
    internal static void WriteEnvironmentFile(CliConfig config)
    {
        const string envPath = "/etc/dotnetcloud/env";
        const string serviceGroup = "dotnetcloud";

        var scheme = config.EnableHttps ? "https" : "http";
        var port = config.EnableHttps ? config.HttpsPort : config.HttpPort;
        var host = config.SelfSignedTlsHost
                   ?? config.LetsEncryptDomain
                   ?? Environment.MachineName
                   ?? "localhost";
        var publicOrigin = $"{scheme}://{host}:{port}";

        try
        {
            var lines = new List<string>
            {
                "# DotNetCloud runtime environment (generated by CLI)",
                "",
                "# Admin credentials (used by AdminSeeder)",
                $"DotNetCloud__AdminEmail={config.AdminEmail ?? ""}",
                "",
                "# Collabora Online",
                $"Files__Collabora__ServerUrl={publicOrigin}",
                $"Files__Collabora__WopiBaseUrl={publicOrigin}",
            };

            // Only write Collabora-specific vars when BuiltIn mode is configured
            if (string.Equals(config.CollaboraMode, "BuiltIn", StringComparison.OrdinalIgnoreCase))
            {
                lines.AddRange(new[]
                {
                    "Files__Collabora__Enabled=true",
                    "Files__Collabora__UseBuiltInCollabora=false",
                    "Files__Collabora__DiscoveryUrl=https://localhost:5443",
                    "Files__Collabora__ProxyUpstreamUrl=https://localhost:9980",
                    "Files__Collabora__AllowInsecureTls=true",
                    $"Files__Collabora__TokenSigningKey={config.WopiTokenSigningKey}",
                });
            }

            File.WriteAllLines(envPath, lines);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(envPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);

                var chown = new System.Diagnostics.ProcessStartInfo("chown", $"root:{serviceGroup} \"{envPath}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var cp = System.Diagnostics.Process.Start(chown);
                cp?.WaitForExit(5000);
            }

            ConsoleOutput.WriteSuccess($"Environment file written to {envPath}.");
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteWarning($"Failed to write environment file: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads a PEM bundle and writes the separate key, cert, and CA chain files
    /// to /etc/coolwsd/, then restarts coolwsd.
    /// </summary>
    private static void ProvisionCollaboraCertsFromPem(string pemPath)
    {
        if (!File.Exists(pemPath))
        {
            ConsoleOutput.WriteWarning($"PEM bundle not found at {pemPath}. Skipping Collabora cert sync.");
            return;
        }

        // Read the PEM bundle content
        var pemContent = File.ReadAllText(pemPath);
        var pemBlocks = new System.Collections.Generic.List<string>();
        var currentBlock = new System.Text.StringBuilder();
        var inBlock = false;

        foreach (var line in pemContent.Split('\n'))
        {
            if (line.StartsWith("-----BEGIN "))
            {
                inBlock = true;
                currentBlock.Clear();
            }

            if (inBlock)
            {
                currentBlock.AppendLine(line);
            }

            if (line.StartsWith("-----END ") && inBlock)
            {
                pemBlocks.Add(currentBlock.ToString().Trim());
                inBlock = false;
            }
        }

        if (pemBlocks.Count < 3)
        {
            ConsoleOutput.WriteWarning($"PEM bundle has {pemBlocks.Count} blocks, expected at least 3 (key, cert, chain). Skipping Collabora cert sync.");
            return;
        }

        // Block order: [0]=private key, [1]=server cert, [2..]=CA chain
        var keyPem = pemBlocks[0];
        var certPem = pemBlocks[1];
        var caChainPem = string.Join("\n", pemBlocks, 2, pemBlocks.Count - 2);

        const string coolwsdCertDir = "/etc/coolwsd";
        const string coolUser = "cool";

        try
        {
            File.WriteAllText(System.IO.Path.Combine(coolwsdCertDir, "key.pem"), keyPem);
            File.WriteAllText(System.IO.Path.Combine(coolwsdCertDir, "cert.pem"), certPem);
            File.WriteAllText(System.IO.Path.Combine(coolwsdCertDir, "ca-chain.cert.pem"), caChainPem);

            // Set ownership and permissions
            if (!OperatingSystem.IsWindows())
            {
                foreach (var file in new[] { "key.pem", "cert.pem", "ca-chain.cert.pem" })
                {
                    var fullPath = System.IO.Path.Combine(coolwsdCertDir, file);
                    File.SetUnixFileMode(fullPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite |
                        UnixFileMode.GroupRead);

                    var chown = new System.Diagnostics.ProcessStartInfo("chown", $"root:{coolUser} \"{fullPath}\"")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    };
                    using var cp = System.Diagnostics.Process.Start(chown);
                    cp?.WaitForExit(5000);
                }
            }

            ConsoleOutput.WriteSuccess("Collabora Online certs updated.");

            // Restart coolwsd
            ConsoleOutput.WriteInfo("Restarting coolwsd to pick up new certificate...");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "restart coolwsd.service",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(30000);

            if (process?.ExitCode == 0)
            {
                ConsoleOutput.WriteSuccess("coolwsd restarted.");
            }
            else
            {
                ConsoleOutput.WriteWarning("Could not restart coolwsd automatically.");
                ConsoleOutput.WriteInfo("Restart manually: sudo systemctl restart coolwsd");
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteWarning($"Failed to sync Collabora certs: {ex.Message}");
            ConsoleOutput.WriteInfo("Restart manually after fixing: sudo systemctl restart coolwsd");
        }
    }
}
