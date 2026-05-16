using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using Certes.Pkcs;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Provisions and renews Let's Encrypt TLS certificates via the ACME protocol.
/// Uses HTTP-01 challenge validation (requires port 80 reachable from the internet).
/// </summary>
internal static class AcmeService
{
    private const string LetsEncryptEndpoint = "https://acme-v02.api.letsencrypt.org/directory";
    private const string LetsEncryptStagingEndpoint = "https://acme-staging-v02.api.letsencrypt.org/directory";

    /// <summary>
    /// Default directory for ACME account and order state persistence.
    /// </summary>
    private static readonly string AcmeStateDir = Path.Combine(
        CliConfiguration.GetConfigDirectory(), "acme");

    /// <summary>
    /// File path for persisting the ACME account key.
    /// </summary>
    private static readonly string AccountKeyPath = Path.Combine(AcmeStateDir, "account-key.pem");

    /// <summary>
    /// Provisions a Let's Encrypt certificate for the specified domain.
    /// Handles account registration, HTTP-01 challenge, and certificate download.
    /// The resulting PFX is saved to the configured cert directory and config is updated.
    /// </summary>
    /// <param name="domain">The domain to provision (e.g., "cloud.example.com").</param>
    /// <param name="contactEmail">Contact email for the ACME account.</param>
    /// <param name="config">The CLI config to update with the certificate path.</param>
    /// <param name="useStaging">If true, use Let's Encrypt staging endpoint for testing.</param>
    /// <returns>True if the certificate was provisioned successfully.</returns>
    public static async Task<bool> ProvisionCertificateAsync(
        string domain,
        string contactEmail,
        CliConfig config,
        bool useStaging = false)
    {
        try
        {
            Directory.CreateDirectory(AcmeStateDir);

            var endpointUri = new Uri(useStaging ? LetsEncryptStagingEndpoint : LetsEncryptEndpoint);
            ConsoleOutput.WriteInfo($"Connecting to Let's Encrypt{(useStaging ? " (STAGING)" : "")}...");

            // If there's an existing certificate for this domain that's still valid, skip
            var existingCertPath = GetCertificatePath(config, domain);
            if (File.Exists(existingCertPath) && !IsCertificateExpiringSoon(existingCertPath))
            {
                ConsoleOutput.WriteInfo($"Existing certificate for {domain} is still valid.");
                return UpdateConfigWithCertPath(config, existingCertPath);
            }

            // Load or create ACME account key
            var accountKey = await LoadOrCreateAccountKeyAsync();

            // Create ACME context
            var context = new AcmeContext(endpointUri, accountKey);
            var accountContext = await LoadOrCreateAccountAsync(context, contactEmail);

            // Order a certificate
            ConsoleOutput.WriteInfo($"Ordering certificate for {domain}...");
            var orderContext = await context.NewOrder(new[] { domain });

            // Get the HTTP-01 challenge details
            var authzContexts = await orderContext.Authorizations();
            var httpChallenge = await GetHttpChallengeAsync(authzContexts, domain);
            if (httpChallenge == null)
            {
                ConsoleOutput.WriteError($"Could not find HTTP-01 challenge for domain '{domain}'.");
                ConsoleOutput.WriteInfo("Ensure the domain resolves to this server and port 80 is reachable.");
                return false;
            }

            var challengeResource = await httpChallenge.Resource();
            var keyAuthz = httpChallenge.KeyAuthz;
            var token = challengeResource.Token;

            // Start temporary HTTP server on port 80 to serve the ACME challenge
            ConsoleOutput.WriteInfo("Starting temporary HTTP challenge server on port 80...");
            using var challengeServer = StartChallengeServer(domain, token, keyAuthz);

            // Notify Let's Encrypt to validate the challenge
            ConsoleOutput.WriteInfo("Notifying Let's Encrypt to validate...");
            await httpChallenge.Validate();

            // Wait for validation to complete
            var maxRetries = 30; // ~30 seconds
            var isValid = false;
            for (var i = 0; i < maxRetries; i++)
            {
                await Task.Delay(1000);
                challengeResource = await httpChallenge.Resource();
                if (challengeResource.Status == Certes.Acme.Resource.ChallengeStatus.Valid)
                {
                    isValid = true;
                    break;
                }

                if (challengeResource.Status == Certes.Acme.Resource.ChallengeStatus.Invalid)
                {
                    var error = challengeResource.Error?.Detail ?? "Unknown error";
                    ConsoleOutput.WriteError($"Let's Encrypt challenge failed: {error}");
                    ConsoleOutput.WriteInfo("Ensure:");
                    ConsoleOutput.WriteInfo($"  - DNS A record for {domain} points to this server's public IP");
                    ConsoleOutput.WriteInfo("  - Port 80 (HTTP) is open and reachable from the internet");
                    ConsoleOutput.WriteInfo("  - No firewall is blocking inbound connections on port 80");
                    return false;
                }
            }

            if (!isValid)
            {
                ConsoleOutput.WriteError("Let's Encrypt validation timed out (30s). Check DNS and firewall.");
                return false;
            }

            ConsoleOutput.WriteSuccess("Let's Encrypt domain validation passed!");

            // Generate a key pair for the certificate and create a CSR
            ConsoleOutput.WriteInfo("Finalizing order and downloading certificate...");
            var certKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var csrBuilder = new CertificationRequestBuilder(certKey);
            csrBuilder.AddName("CN", domain);
            csrBuilder.SubjectAlternativeNames.Add(domain);
            var csrBytes = csrBuilder.Generate();

            // Finalize the order with the CSR
            await orderContext.Finalize(csrBytes);

            // Wait for the order to be ready
            await WaitForOrderReadyAsync(orderContext);

            // Download the certificate
            var certificateChain = await orderContext.Download();

            var pfxBuilder = certificateChain.ToPfx(certKey);
            pfxBuilder.FullChain = true;
            var pfxBytes = pfxBuilder.Build($"DotNetCloud - {domain}", string.Empty);

            var certDir = Path.GetDirectoryName(existingCertPath)!;
            Directory.CreateDirectory(certDir);

            // Write PFX file
            await File.WriteAllBytesAsync(existingCertPath, pfxBytes);

            // Set restrictive permissions on Linux
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(existingCertPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
                SystemdServiceHelper.FixOwnership(existingCertPath);
            }

            ConsoleOutput.WriteSuccess($"Certificate saved to {existingCertPath}");

            // Also save the full chain PEM for reference
            var pemPath = Path.ChangeExtension(existingCertPath, ".pem");
            var pemExport = certificateChain.ToPem(certKey);
            await File.WriteAllTextAsync(pemPath, pemExport);

            return UpdateConfigWithCertPath(config, existingCertPath);
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Let's Encrypt certificate provisioning failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if an existing certificate needs renewal (expires within 30 days).
    /// </summary>
    public static bool IsCertificateExpiringSoon(string certPath)
    {
        try
        {
            using var cert = X509CertificateLoader.LoadCertificate(File.ReadAllBytes(certPath));
            return cert.NotAfter <= DateTime.UtcNow.AddDays(30);
        }
        catch
        {
            return true; // If we can't read it, treat as expiring
        }
    }

    /// <summary>
    /// Gets the certificate path for Let's Encrypt certificates.
    /// </summary>
    public static string GetCertificatePath(CliConfig config, string domain)
    {
        var certDir = CliConfiguration.IsSystemInstall
            ? Path.Combine(CliConfiguration.GetConfigDirectory(), "certs")
            : Path.Combine(config.DataDirectory, "certs");

        return Path.Combine(certDir, $"dotnetcloud-le-{domain}.pfx");
    }

    /// <summary>
    /// Returns the Certes ACME directory URI based on environment.
    /// </summary>
    public static Uri GetDirectoryUri(bool useStaging = false)
    {
        return new Uri(useStaging ? LetsEncryptStagingEndpoint : LetsEncryptEndpoint);
    }

    /// <summary>
    /// Validates that the domain resolves to this machine.
    /// </summary>
    public static bool CanDomainResolveToLocalMachine(string domain)
    {
        try
        {
            var addresses = Dns.GetHostAddresses(domain);
            var localAddresses = Dns.GetHostAddresses(Dns.GetHostName())
                .Concat(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
                .ToHashSet();

            // Also check common local network interfaces
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        localAddresses.Add(addr.Address);
                    }
                }
            }

            return addresses.Any(addr => localAddresses.Contains(addr));
        }
        catch
        {
            return false; // Domain doesn't resolve at all
        }
    }

    /// <summary>
    /// Checks if port 80 is available for the ACME HTTP-01 challenge.
    /// </summary>
    public static bool IsPort80Available()
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Any, 80);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<IKey> LoadOrCreateAccountKeyAsync()
    {
        if (File.Exists(AccountKeyPath))
        {
            try
            {
                var pem = await File.ReadAllTextAsync(AccountKeyPath);
                var key = KeyFactory.FromPem(pem);
                ConsoleOutput.WriteInfo("Using existing Let's Encrypt account.");
                return key;
            }
            catch
            {
                // Fall through to create new key
            }
        }

        ConsoleOutput.WriteInfo("Generating new Let's Encrypt account key...");
        var accountKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
        var pemKey = accountKey.ToPem();
        await File.WriteAllTextAsync(AccountKeyPath, pemKey);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(AccountKeyPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
            SystemdServiceHelper.FixOwnership(AccountKeyPath);
        }

        return accountKey;
    }

    private static async Task<IAccountContext> LoadOrCreateAccountAsync(
        IAcmeContext context, string contactEmail)
    {
        try
        {
            var account = await context.Account();
            ConsoleOutput.WriteInfo("Using existing Let's Encrypt account.");
            return account;
        }
        catch
        {
            // Account doesn't exist yet — create new one
        }

        ConsoleOutput.WriteInfo("Registering new Let's Encrypt account...");
        return await context.NewAccount(new[] { $"mailto:{contactEmail}" }, true);
    }

    private static async Task<IChallengeContext?> GetHttpChallengeAsync(
        IEnumerable<IAuthorizationContext> authorizations, string domain)
    {
        foreach (var authz in authorizations)
        {
            var authzResult = await authz.Resource();
            if (string.Equals(authzResult.Identifier.Value, domain, StringComparison.OrdinalIgnoreCase))
            {
                return await authz.Http();
            }
        }

        return null;
    }

    private static async Task WaitForOrderReadyAsync(IOrderContext order)
    {
        var maxRetries = 15;
        for (var i = 0; i < maxRetries; i++)
        {
            await Task.Delay(2000);
            var orderResource = await order.Resource();
            if (orderResource.Status == Certes.Acme.Resource.OrderStatus.Ready ||
                orderResource.Status == Certes.Acme.Resource.OrderStatus.Valid)
            {
                return;
            }
        }
    }

    private static TcpListener? StartChallengeServer(
        string domain, string token, string keyAuthz)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Any, 80);
            listener.Start();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        _ = HandleChallengeRequestAsync(client, token, keyAuthz);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped
                }
                catch (SocketException)
                {
                    // Listener was stopped
                }
            });

            return listener;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteWarning($"Could not start HTTP challenge server on port 80: {ex.Message}");
            ConsoleOutput.WriteInfo("Ensure port 80 is not in use by another process.");
            return null;
        }
    }

    private static async Task HandleChallengeRequestAsync(
        TcpClient client, string token, string keyAuthz)
    {
        try
        {
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream);

            // Read the HTTP request line
            var requestLine = await reader.ReadLineAsync();
            if (requestLine == null) return;

            // Skip remaining request headers
            string? header;
            do
            {
                header = await reader.ReadLineAsync();
            }
            while (header != null && header != "");

            // Build response
            var challengePath = $"/.well-known/acme-challenge/{token}";
            var parts = requestLine.Split(' ');
            var requestedPath = parts.Length > 1 ? parts[1] : "/";

            var body = System.Text.Encoding.UTF8.GetBytes(
                requestedPath == challengePath ? keyAuthz : "Not Found");
            var statusLine = requestedPath == challengePath
                ? "HTTP/1.1 200 OK\r\n"
                : "HTTP/1.1 404 Not Found\r\n";
            var headers = $"Content-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";

            var responseBytes = System.Text.Encoding.UTF8.GetBytes(statusLine + headers)
                .Concat(body)
                .ToArray();

            await stream.WriteAsync(responseBytes);
        }
        catch
        {
            // Client disconnected — ignore
        }
    }

    private static bool UpdateConfigWithCertPath(CliConfig config, string certPath)
    {
        config.TlsCertificatePath = certPath;
        try
        {
            CliConfiguration.Save(config);
            return true;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Failed to save config with certificate path: {ex.Message}");
            return false;
        }
    }
}
