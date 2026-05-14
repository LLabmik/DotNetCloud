using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace DotNetCloud.Core.Auth.Security;

/// <summary>
/// Manages persistent RSA keys for OpenIddict token signing and encryption.
/// Keys are stored as PEM files so they survive server restarts.
/// Supports automatic rotation via date-stamped filenames.
/// </summary>
public static class OidcKeyManager
{
    private const int KeySizeInBits = 2048;

    /// <summary>Default filename for the initial signing key (backward compatible).</summary>
    public const string SigningKeyPrefix = "signing-key";

    /// <summary>Default filename for the initial encryption key (backward compatible).</summary>
    public const string EncryptionKeyPrefix = "encryption-key";

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
            SetSecureFilePermissions(filePath);
            logger?.LogInformation("Generated new OpenIddict RSA key at {KeyFile}.", filePath);
        }

        return new RsaSecurityKey(rsa);
    }

    /// <summary>
    /// Loads all RSA keys from the given directory matching the specified file prefix.
    /// Useful for supporting multiple keys after automated rotation.
    /// </summary>
    /// <param name="directory">Directory containing key files.</param>
    /// <param name="filePrefix">Filename prefix (e.g., "signing-key" or "encryption-key").</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>List of loaded security keys, sorted by file creation time (newest first).</returns>
    public static List<RsaSecurityKey> LoadAllKeys(string directory, string filePrefix, ILogger? logger = null)
    {
        var keys = new List<RsaSecurityKey>();

        if (!Directory.Exists(directory))
        {
            return keys;
        }

        foreach (var file in Directory.GetFiles(directory, $"{filePrefix}*.pem")
                     .OrderByDescending(f => f))
        {
            try
            {
                var pem = File.ReadAllText(file);
                var rsa = RSA.Create();
                rsa.ImportFromPem(pem);
                keys.Add(new RsaSecurityKey(rsa));
                logger?.LogTrace("Loaded OpenIddict key from {KeyFile}.", file);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load key file {KeyFile}, skipping.", file);
            }
        }

        return keys;
    }

    /// <summary>
    /// Generates a new versioned RSA key file in the specified directory.
    /// The filename includes the current date to enable rotation tracking.
    /// </summary>
    /// <param name="directory">Directory to write the key file.</param>
    /// <param name="filePrefix">Filename prefix (e.g., "signing-key" or "encryption-key").</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>The generated security key.</returns>
    public static RsaSecurityKey GenerateRotatedKey(string directory, string filePrefix, ILogger? logger = null)
    {
        Directory.CreateDirectory(directory);

        var dateStamp = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var counter = 0;
        string filePath;

        do
        {
            var suffix = counter == 0 ? dateStamp : $"{dateStamp}-{counter}";
            filePath = Path.Combine(directory, $"{filePrefix}-{suffix}.pem");
            counter++;
        }
        while (File.Exists(filePath));

        var rsa = RSA.Create(KeySizeInBits);
        var pem = rsa.ExportRSAPrivateKeyPem();
        File.WriteAllText(filePath, pem);
        SetSecureFilePermissions(filePath);

        logger?.LogInformation("Generated rotated OpenIddict key at {KeyFile}.", filePath);
        return new RsaSecurityKey(rsa);
    }

    /// <summary>
    /// Gets the newest key file matching the given prefix, or null if none exist.
    /// </summary>
    public static string? GetNewestKeyFile(string directory, string filePrefix)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.GetFiles(directory, $"{filePrefix}*.pem")
            .OrderByDescending(f => f)
            .FirstOrDefault();
    }

    /// <summary>
    /// Cleans up key files older than the specified retention period.
    /// Always keeps at least one key (the newest) even if it exceeds the retention period.
    /// </summary>
    /// <param name="directory">Directory containing key files.</param>
    /// <param name="filePrefix">Filename prefix.</param>
    /// <param name="retentionPeriod">Keys older than this are removed.</param>
    /// <param name="logger">Optional logger.</param>
    public static void CleanupOldKeys(string directory, string filePrefix, TimeSpan retentionPeriod, ILogger? logger = null)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var cutoff = DateTime.UtcNow - retentionPeriod;
        var files = Directory.GetFiles(directory, $"{filePrefix}*.pem")
            .OrderByDescending(f => f)
            .ToList();

        // Always keep at least the newest key
        if (files.Count <= 1)
        {
            return;
        }

        foreach (var file in files.Skip(1)) // Skip the newest
        {
            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(file);
                if (lastWrite < cutoff)
                {
                    File.Delete(file);
                    logger?.LogInformation("Removed expired OpenIddict key {KeyFile}.", file);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to clean up key file {KeyFile}, skipping.", file);
            }
        }
    }

    /// <summary>
    /// Determines whether a new signing key should be generated based on rotation schedule.
    /// </summary>
    /// <param name="directory">Directory containing key files.</param>
    /// <param name="filePrefix">Filename prefix.</param>
    /// <param name="rotationInterval">How often to generate new keys.</param>
    /// <returns>True if a new key should be generated.</returns>
    public static bool ShouldRotate(string directory, string filePrefix, TimeSpan rotationInterval)
    {
        var newest = GetNewestKeyFile(directory, filePrefix);
        if (newest == null)
        {
            return true; // No key exists, need to create one
        }

        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(newest);
        return age >= rotationInterval;
    }

    private static void SetSecureFilePermissions(string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(filePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
