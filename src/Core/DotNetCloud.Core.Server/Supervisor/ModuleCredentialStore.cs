using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Holds the per-module tokens that the process supervisor mints when it launches a module host.
/// The module must present its token on every core call, which binds the <c>dnc-module-id</c> the
/// call claims to a credential only the core could have handed out.
/// </summary>
/// <remarks>
/// The store is intentionally in-memory: the credential's only job is to authenticate a live
/// module process, and a restart of the core re-issues fresh tokens to every module it spawns.
/// A restart of an individual module also rotates its token, so a stale process cannot keep
/// calling the core.
/// </remarks>
internal sealed class ModuleCredentialStore
{
    private readonly ConcurrentDictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Mints a fresh token for <paramref name="moduleId"/>, replacing any previously issued
    /// token for that module.
    /// </summary>
    /// <param name="moduleId">The module identifier to issue a credential for.</param>
    /// <returns>The generated token, to be passed to the module process.</returns>
    public string Issue(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _tokens[moduleId] = token;
        return token;
    }

    /// <summary>
    /// Gets the token currently issued for <paramref name="moduleId"/>.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="token">The issued token when one exists.</param>
    /// <returns><see langword="true"/> when a credential is currently active.</returns>
    public bool TryGetToken(string moduleId, out string token)
    {
        token = string.Empty;
        return !string.IsNullOrWhiteSpace(moduleId) && _tokens.TryGetValue(moduleId, out token!);
    }

    /// <summary>
    /// Revokes the token issued for <paramref name="moduleId"/>, if any.
    /// </summary>
    /// <param name="moduleId">The module identifier to revoke.</param>
    public void Revoke(string moduleId)
    {
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            _tokens.TryRemove(moduleId, out _);
        }
    }

    /// <summary>
    /// Determines whether a token has been issued for <paramref name="moduleId"/>.
    /// </summary>
    /// <param name="moduleId">The module identifier to check.</param>
    /// <returns><see langword="true"/> when a credential is currently active.</returns>
    public bool HasCredential(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && _tokens.ContainsKey(moduleId);

    /// <summary>
    /// Validates a presented token against the one issued for <paramref name="moduleId"/>
    /// using a fixed-time comparison.
    /// </summary>
    /// <param name="moduleId">The module identifier claimed by the caller.</param>
    /// <param name="presentedToken">The token the caller supplied.</param>
    /// <returns><see langword="true"/> only when the token matches the issued credential.</returns>
    public bool Validate(string moduleId, string? presentedToken)
    {
        if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrEmpty(presentedToken))
        {
            return false;
        }

        if (!_tokens.TryGetValue(moduleId, out var expectedToken))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(expectedToken);
        var actual = Encoding.UTF8.GetBytes(presentedToken);
        return actual.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
