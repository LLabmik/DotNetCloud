using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace DotNetCloud.Core.Auth.Security;

/// <summary>
/// Manages persistent RSA keys for OpenIddict token signing and encryption.
/// Keys are stored as PEM files so they survive server restarts.
/// </summary>
public static class OidcKeyManager
{
    private const int KeySizeInBits = 2048;

    /// <summary>
    /// Loads or generates a persistent RSA security key from a PEM file.
    /// </summary>
    /// <param name="filePath">Absolute path to the PEM key file.</param>
    /// <param name="logger">Optional logger for key creation events.</param>
    /// <returns>An <see cref="RsaSecurityKey"/> loaded from <paramref name="filePath"/>.</returns>
    public static RsaSecurityKey LoadOrCreateKey(string filePath, ILogger? logger = null)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        RSA rsa;

        if (File.Exists(filePath))
        {
            var pem = File.ReadAllText(filePath);
            rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            logger?.LogDebug("Loaded OpenIddict RSA key from {KeyFile}.", filePath);
        }
        else
        {
            rsa = RSA.Create(KeySizeInBits);
            var pem = rsa.ExportRSAPrivateKeyPem();
            File.WriteAllText(filePath, pem);
            // Restrict permissions to owner-only on Linux/macOS.
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(filePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            logger?.LogInformation("Generated new OpenIddict RSA key at {KeyFile}.", filePath);
        }

        return new RsaSecurityKey(rsa);
    }
}
