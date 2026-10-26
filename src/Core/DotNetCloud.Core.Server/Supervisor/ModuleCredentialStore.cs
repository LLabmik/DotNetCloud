using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Holds the per-module shared secrets that the process supervisor mints when it launches a
/// module host. The module must present its secret on every capability call, which binds the
/// <c>module-id</c> the call claims to a credential only the core could have handed out.
/// </summary>
/// <remarks>
/// The store is intentionally in-memory: the credential's only job is to authenticate a live
/// module process, and a restart of the core re-issues fresh secrets to every module it spawns.
/// A restart of an individual module also rotates its secret, so a stale process cannot keep
/// calling the core.
/// </remarks>
internal sealed class ModuleCredentialStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Mints a fresh secret for <paramref name="moduleId"/>, replacing any previously issued
    /// secret for that module.
    /// </summary>
    /// <param name="moduleId">The module identifier to issue a credential for.</param>
    /// <returns>The generated secret, to be passed to the module process.</returns>
    public string Issue(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _secrets[moduleId] = secret;
        return secret;
    }

    /// <summary>
    /// Gets the secret currently issued for <paramref name="moduleId"/>.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="secret">The issued secret when one exists.</param>
    /// <returns><see langword="true"/> when a credential is currently active.</returns>
    public bool TryGetSecret(string moduleId, out string secret)
    {
        secret = string.Empty;
        return !string.IsNullOrWhiteSpace(moduleId) && _secrets.TryGetValue(moduleId, out secret!);
    }

    /// <summary>
    /// Revokes the secret issued for <paramref name="moduleId"/>, if any.
    /// </summary>
    /// <param name="moduleId">The module identifier to revoke.</param>
    public void Revoke(string moduleId)
    {
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            _secrets.TryRemove(moduleId, out _);
        }
    }

    /// <summary>
    /// Determines whether a secret has been issued for <paramref name="moduleId"/>.
    /// </summary>
    /// <param name="moduleId">The module identifier to check.</param>
    /// <returns><see langword="true"/> when a credential is currently active.</returns>
    public bool HasCredential(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && _secrets.ContainsKey(moduleId);

    /// <summary>
    /// Validates a presented secret against the one issued for <paramref name="moduleId"/>
    /// using a fixed-time comparison.
    /// </summary>
    /// <param name="moduleId">The module identifier claimed by the caller.</param>
    /// <param name="presentedSecret">The secret the caller supplied.</param>
    /// <returns><see langword="true"/> only when the secret matches the issued credential.</returns>
    public bool Validate(string moduleId, string? presentedSecret)
    {
        if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrEmpty(presentedSecret))
        {
            return false;
        }

        if (!_secrets.TryGetValue(moduleId, out var expectedSecret))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(expectedSecret);
        var actual = Encoding.UTF8.GetBytes(presentedSecret);
        return actual.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
