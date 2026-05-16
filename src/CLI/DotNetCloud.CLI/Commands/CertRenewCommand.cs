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
}
