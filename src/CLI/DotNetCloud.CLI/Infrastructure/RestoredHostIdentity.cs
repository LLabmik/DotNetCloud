using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>What a restore had to change to match the machine it landed on.</summary>
internal sealed record HostRepointResult(
    string OldHost,
    string NewHost,
    bool CertificateReissued,
    bool RootCaChanged,
    bool CollaboraUpdated,
    IReadOnlyList<string> WrittenFiles);

/// <summary>Side effects of a re-point, injectable so the logic can be tested without touching the host.</summary>
internal sealed record HostRepointOptions(
    Action<CliConfig> Persist,
    Action<string, string>? SetOwnerAndMode = null,
    string? CollaboraDirectory = null,
    bool RestartCollabora = true);

/// <summary>
/// Re-points a restored installation at the machine it was restored onto.
///
/// A backup carries the previous machine's identity on purpose: its configuration, its TLS
/// certificate and its Collabora WOPI alias group all name the old host. On the same machine that is
/// exactly right; on replacement hardware it produces a certificate-name mismatch in every browser
/// and a Collabora that rejects the new origin. This class compares the restored identity with the
/// current host name and, when they differ, re-issues the server certificate from the restored root
/// CA (so clients that already trust that root stay trusted) and rewrites the Collabora alias group.
/// </summary>
internal static class RestoredHostIdentity
{
    private const string RootSubject = "CN=DotNetCloud Local Root CA";
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";
    private const string ManagedStart = "dotnetcloud-managed-start";
    private const string ManagedEnd = "dotnetcloud-managed-end";

    /// <summary>Host name of the machine this process is running on.</summary>
    internal static string GetCurrentHost() => Dns.GetHostName();

    /// <summary>
    /// Compares the restored identity with the current host name and re-issues what is bound to the old
    /// name. Returns <see langword="null"/> when nothing had to change.
    /// </summary>
    /// <param name="config">Restored configuration (updated and saved when its host name is stale).</param>
    /// <param name="serviceUser">Account that owns the configuration directory.</param>
    /// <param name="warn">Receives human-readable warnings.</param>
    /// <param name="options">Side effects (persistence, ownership, Collabora); defaults are the real ones.</param>
    internal static HostRepointResult? Repoint(
        CliConfig config,
        string serviceUser,
        Action<string> warn,
        HostRepointOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(warn);

        options ??= new HostRepointOptions(CliConfiguration.Save);

        if (!config.EnableHttps)
            return null;

        var host = GetCurrentHost();

        if (!config.UseSelfSignedTls)
        {
            var domain = config.LetsEncryptDomain?.Trim();
            if (!string.IsNullOrWhiteSpace(domain) && !HostMatches(domain, host))
            {
                warn($"The restored HTTPS certificate is for '{domain}', but this machine is named '{host}'. " +
                     "Point DNS for that name at this machine, or run 'dotnetcloud setup' to issue a " +
                     "certificate for the new name.");
            }

            return null;
        }

        var certPath = ResolveCertificatePath(config);
        if (!File.Exists(certPath))
        {
            warn($"The restored self-signed certificate is missing ({certPath}); " +
                 "run 'dotnetcloud setup' to generate one for this machine.");
            return null;
        }

        var restored = X509CertificateLoader.LoadPkcs12CollectionFromFile(certPath, string.Empty);
        var leaf = FindServerCertificate(restored);
        if (leaf is null)
        {
            warn($"No server certificate found in {certPath}; run 'dotnetcloud setup' to regenerate it.");
            return null;
        }

        var coveredNames = GetDnsNames(leaf);
        var oldHost = !string.IsNullOrWhiteSpace(config.SelfSignedTlsHost)
            ? config.SelfSignedTlsHost.Trim()
            : coveredNames.FirstOrDefault(n => !n.Equals("localhost", StringComparison.OrdinalIgnoreCase))
              ?? host;

        if (coveredNames.Any(name => HostMatches(name, host)))
        {
            // Same machine (or a name that is already covered): only the recorded host name can be stale.
            if (!string.Equals(oldHost, host, StringComparison.OrdinalIgnoreCase))
            {
                config.SelfSignedTlsHost = host;
                options.Persist(config);
            }

            return null;
        }

        // Re-issue the leaf for this machine. Keeping the restored root CA means every device that
        // already trusts the old install keeps trusting the new one — no client changes needed.
        var rootCaChanged = false;
        X509Certificate2? createdRoot = null;
        var issuer = restored.Cast<X509Certificate2>().FirstOrDefault(c => IsCertificateAuthority(c) && c.HasPrivateKey);
        if (issuer is null)
        {
            createdRoot = CreateRootCa();
            issuer = createdRoot;
            rootCaChanged = true;
            warn("The restored root CA has no private key, so a new root CA was generated. " +
                 "Devices that trusted the old root CA must install the new one.");
        }

        try
        {
            void Apply(string path, string group)
            {
                TrySetMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
                options.SetOwnerAndMode?.Invoke(path, group);
            }

            var written = new List<string>();
            using var newLeaf = CreateServerCertificate(host, issuer);

            var certificateDirectory = Path.GetDirectoryName(Path.GetFullPath(certPath))!;
            Directory.CreateDirectory(certificateDirectory);

            WriteFileAtomic(certPath, ExportPfx(newLeaf, issuer));
            written.Add(certPath);
            Apply(certPath, serviceUser);

            var pemPath = Path.ChangeExtension(certPath, ".pem");
            WriteFileAtomic(pemPath, BuildPemBundle(newLeaf, issuer, includePrivateKey: true));
            written.Add(pemPath);
            Apply(pemPath, serviceUser);

            var rootCaPath = Path.Combine(certificateDirectory, "dotnetcloud-root-ca.crt");
            WriteFileAtomic(rootCaPath, issuer.ExportCertificatePem());
            written.Add(rootCaPath);
            Apply(rootCaPath, serviceUser);

            var collaboraUpdated = UpdateCollabora(host, oldHost, newLeaf, written, options, warn);

            config.SelfSignedTlsHost = host;
            options.Persist(config);

            return new HostRepointResult(
                oldHost, host, CertificateReissued: true, rootCaChanged, collaboraUpdated, written);
        }
        finally
        {
            createdRoot?.Dispose();
        }
    }

    /// <summary>Resolves the self-signed certificate path, defaulting to the standard configuration location.</summary>
    internal static string ResolveCertificatePath(CliConfig config)
        => string.IsNullOrWhiteSpace(config.TlsCertificatePath)
            ? Path.Combine(CliConfiguration.GetConfigDirectory(), "certs", "dotnetcloud-selfsigned.pfx")
            : config.TlsCertificatePath;

    /// <summary>DNS names published by a certificate's subject alternative name extension.</summary>
    internal static IReadOnlyList<string> GetDnsNames(X509Certificate2 certificate)
    {
        var names = new List<string>();

        var san = certificate.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .FirstOrDefault();

        if (san is not null)
            names.AddRange(san.EnumerateDnsNames().Where(n => !string.IsNullOrWhiteSpace(n)));

        if (names.Count == 0)
        {
            var commonName = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            if (!string.IsNullOrWhiteSpace(commonName))
                names.Add(commonName);
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Returns whether a certificate covers a host name (case-insensitive, trailing dot ignored).</summary>
    internal static bool HostCoveredBy(X509Certificate2 certificate, string host)
        => GetDnsNames(certificate).Any(name => HostMatches(name, host));

    /// <summary>Creates the server (leaf) certificate for <paramref name="host"/>, signed by <paramref name="issuer"/>.</summary>
    internal static X509Certificate2 CreateServerCertificate(string host, X509Certificate2 issuer)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={host}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new(ServerAuthOid) }, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        // Loopback stays on the certificate: module gRPC clients and Blazor loopback calls validate
        // against https://localhost, and they must keep working after the machine is renamed.
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(host);
        san.AddDnsName("localhost");
        san.AddIpAddress(System.Net.IPAddress.Loopback);
        san.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(san.Build());

        using var issued = request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(2),
            RandomNumberGenerator.GetBytes(16));

        return issued.CopyWithPrivateKey(rsa);
    }

    /// <summary>Creates a root CA (used only when the restored root CA cannot sign).</summary>
    internal static X509Certificate2 CreateRootCa()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(RootSubject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(10));
    }

    /// <summary>
    /// PEM bundle in the layout <c>setup</c> writes: server private key, server certificate, root CA.
    /// </summary>
    internal static string BuildPemBundle(X509Certificate2 leaf, X509Certificate2 root, bool includePrivateKey)
    {
        var builder = new StringBuilder();

        if (includePrivateKey)
        {
            using var key = leaf.GetRSAPrivateKey()
                ?? throw new InvalidOperationException("The server certificate has no RSA private key.");
            builder.Append(key.ExportRSAPrivateKeyPem());
        }

        builder.Append(leaf.ExportCertificatePem());
        builder.Append(root.ExportCertificatePem());
        return builder.ToString();
    }

    /// <summary>
    /// Rewrites the DotNetCloud-managed Collabora alias group so the new host name is accepted.
    /// Only the block between the managed markers is touched.
    /// </summary>
    internal static (string Content, bool Changed) UpdateCollaboraAliasGroup(string xml, string oldHost, string newHost)
    {
        if (string.IsNullOrEmpty(xml) || string.IsNullOrWhiteSpace(oldHost) || string.IsNullOrWhiteSpace(newHost))
            return (xml, false);

        var start = xml.IndexOf(ManagedStart, StringComparison.Ordinal);
        var end = xml.IndexOf(ManagedEnd, StringComparison.Ordinal);
        if (start < 0 || end <= start)
            return (xml, false);

        var endExclusive = Math.Min(xml.Length, end + ManagedEnd.Length);
        var block = xml[start..endExclusive];
        var updatedBlock = Regex.Replace(
            block,
            $@"(?<![A-Za-z0-9.-]){Regex.Escape(oldHost)}(?![A-Za-z0-9-])",
            newHost,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (updatedBlock.Equals(block, StringComparison.Ordinal))
            return (xml, false);

        return (xml[..start] + updatedBlock + xml[endExclusive..], true);
    }

    /// <summary>
    /// Updates Collabora's certificate files and alias group when the host changed. Best-effort: a
    /// missing or unreadable Collabora install leaves the restore unaffected.
    /// </summary>
    private static bool UpdateCollabora(
        string host,
        string oldHost,
        X509Certificate2 leaf,
        List<string> written,
        HostRepointOptions options,
        Action<string> warn)
    {
        var directory = options.CollaboraDirectory ?? BackupArchive.DefaultCoolwsdDirectory;
        if (!OperatingSystem.IsLinux() || !Directory.Exists(directory))
            return false;

        var group = GroupExists("cool") ? "cool" : "root";
        var updated = false;

        var configPath = Path.Combine(directory, "coolwsd.xml");
        if (File.Exists(configPath))
        {
            try
            {
                var xml = File.ReadAllText(configPath);
                var (content, changed) = UpdateCollaboraAliasGroup(xml, oldHost, host);
                if (changed)
                {
                    var temp = configPath + ".tmp-" + Guid.NewGuid().ToString("N");
                    File.WriteAllText(temp, content);
                    File.Move(temp, configPath, overwrite: true);
                    TrySetMode(configPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
                    options.SetOwnerAndMode?.Invoke(configPath, group);
                    written.Add(configPath);
                    updated = true;
                }
            }
            catch (Exception ex)
            {
                warn($"Could not update the Collabora WOPI alias group for '{host}': {ex.Message}");
            }
        }

        // coolwsd presents this certificate to the server; it must match the new leaf.
        var leafPem = leaf.ExportCertificatePem();
        foreach (var name in new[] { "cert.pem", "ca-chain.cert.pem" })
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path))
                continue;

            try
            {
                WriteFileAtomic(path, leafPem);
                TrySetMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
                options.SetOwnerAndMode?.Invoke(path, group);
                written.Add(path);
                updated = true;
            }
            catch (Exception ex)
            {
                warn($"Could not update {path}: {ex.Message}");
            }
        }

        if (updated && options.RestartCollabora)
            RunProcess("systemctl", ["try-restart", "coolwsd"], null, out _, out _);

        return updated;
    }

    private static X509Certificate2? FindServerCertificate(X509Certificate2Collection collection)
    {
        var candidates = collection.Cast<X509Certificate2>().Where(c => !IsCertificateAuthority(c)).ToArray();
        if (candidates.Length == 0)
            return null;

        bool HasServerAuth(X509Certificate2 certificate) => certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .Any(e => e.EnhancedKeyUsages.Cast<Oid>().Any(o => o.Value == ServerAuthOid));

        return candidates.FirstOrDefault(HasServerAuth) ?? candidates[0];
    }

    private static bool IsCertificateAuthority(X509Certificate2 certificate)
        => certificate.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .Any(e => e.CertificateAuthority);

    private static bool HostMatches(string name, string host)
        => string.Equals(name.TrimEnd('.'), host.TrimEnd('.'), StringComparison.OrdinalIgnoreCase);

    private static byte[] ExportPfx(X509Certificate2 leaf, X509Certificate2 root)
        => new X509Certificate2Collection { leaf, root }.Export(X509ContentType.Pfx, string.Empty)
           ?? throw new InvalidOperationException("Could not export the certificate.");

    private static void WriteFileAtomic(string path, byte[] content)
    {
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            stream.Write(content);

        File.Move(temp, path, overwrite: true);
    }

    private static void WriteFileAtomic(string path, string content)
    {
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    private static void TrySetMode(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows() || !Path.Exists(path))
            return;

        try
        { File.SetUnixFileMode(path, mode); }
        catch { /* best effort */ }
    }

    private static bool GroupExists(string group)
        => OperatingSystem.IsLinux() && RunProcess("getent", ["group", group], null, out _, out _);

    private static bool RunProcess(
        string fileName,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string>? environment,
        out string standardOutput,
        out string standardError)
    {
        standardOutput = string.Empty;
        standardError = string.Empty;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            if (environment is not null)
            {
                foreach (var (key, value) in environment)
                    startInfo.Environment[key] = value;
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            standardOutput = outputTask.GetAwaiter().GetResult();
            standardError = errorTask.GetAwaiter().GetResult();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            standardError = ex.Message;
            return false;
        }
    }
}
