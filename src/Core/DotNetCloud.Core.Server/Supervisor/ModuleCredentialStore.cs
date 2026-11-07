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
/// <para>
/// The store is intentionally in-memory (decision D1): the credential's only job is to authenticate
/// a live module process, and a restart of the core re-issues fresh tokens to every module it
/// spawns. A restart of an individual module rotates its token too, so a stale process cannot keep
/// calling the core. Nothing is persisted, so a database backup contains no module secrets.
/// </para>
/// <para>
/// For auditability the store keeps a non-secret lifecycle record per module (hint, issued-at,
/// last-rotated-at, revoked-at). The token itself is never exposed by <see cref="GetStatus"/> and
/// never logged; only the first characters appear, as a hint for correlating log lines.
/// </para>
/// </remarks>
internal sealed class ModuleCredentialStore
{
    /// <summary>Number of leading token characters kept as a non-secret hint.</summary>
    internal const int HintLength = 4;

    private readonly ConcurrentDictionary<string, CredentialEntry> _credentials =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ModuleCredentialStore"/> class.</summary>
    /// <param name="timeProvider">Clock used for lifecycle timestamps; defaults to the system clock.</param>
    public ModuleCredentialStore(TimeProvider? timeProvider = null) =>
        _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Mints a fresh token for <paramref name="moduleId"/>, replacing any previously issued token.
    /// </summary>
    /// <param name="moduleId">The module identifier to issue a credential for.</param>
    /// <returns>The generated token, to be passed to the module process.</returns>
    public string Issue(string moduleId) => Mint(moduleId, isRotation: false);

    /// <summary>
    /// Rotates the token for <paramref name="moduleId"/>: the previous token stops working
    /// immediately and the lifecycle record keeps the original issue time plus a rotated-at stamp.
    /// </summary>
    /// <param name="moduleId">The module identifier to rotate.</param>
    /// <returns>The new token.</returns>
    public string Rotate(string moduleId) => Mint(moduleId, isRotation: true);

    /// <summary>
    /// Gets the token currently issued for <paramref name="moduleId"/>.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="token">The issued token when one is active.</param>
    /// <returns><see langword="true"/> when a credential is currently active.</returns>
    public bool TryGetToken(string moduleId, out string token)
    {
        token = string.Empty;

        if (string.IsNullOrWhiteSpace(moduleId)
            || !_credentials.TryGetValue(moduleId, out var entry)
            || entry.Token is null)
        {
            return false;
        }

        token = entry.Token;
        return true;
    }

    /// <summary>
    /// Revokes the token issued for <paramref name="moduleId"/>, if any. The lifecycle record is
    /// kept (with a revoked-at stamp and no token) so the revocation stays observable.
    /// </summary>
    /// <param name="moduleId">The module identifier to revoke.</param>
    public void Revoke(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        _credentials.AddOrUpdate(
            moduleId,
            _ => new CredentialEntry(null, Hint: null, IssuedAtUtc: null, RotatedAtUtc: null, RevokedAtUtc: now),
            (_, existing) => existing with { Token = null, RevokedAtUtc = now });
    }

    /// <summary>
    /// Determines whether a token has been issued for <paramref name="moduleId"/>.
    /// </summary>
    /// <param name="moduleId">The module identifier to check.</param>
    /// <returns><see langword="true"/> when a credential is currently active.</returns>
    public bool HasCredential(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId)
        && _credentials.TryGetValue(moduleId, out var entry)
        && entry.Token is not null;

    /// <summary>
    /// Gets the non-secret lifecycle record for a module's credential.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <returns>The record, or <see langword="null"/> when no credential was ever issued.</returns>
    public ModuleCredentialStatus? GetStatus(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && _credentials.TryGetValue(moduleId, out var entry)
            ? new ModuleCredentialStatus(
                moduleId,
                entry.Hint,
                entry.IssuedAtUtc,
                entry.RotatedAtUtc,
                entry.RevokedAtUtc,
                IsActive: entry.Token is not null)
            : null;

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

        if (!_credentials.TryGetValue(moduleId, out var entry) || entry.Token is null)
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(entry.Token);
        var actual = Encoding.UTF8.GetBytes(presentedToken);
        return actual.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private string Mint(string moduleId, bool isRotation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hint = token.Length > HintLength ? token[..HintLength] : token;
        var now = _timeProvider.GetUtcNow();

        _credentials.AddOrUpdate(
            moduleId,
            _ => new CredentialEntry(token, hint, now, isRotation ? now : null, RevokedAtUtc: null),
            (_, existing) => existing with
            {
                Token = token,
                Hint = hint,
                IssuedAtUtc = existing.IssuedAtUtc ?? now,
                RotatedAtUtc = isRotation ? now : existing.RotatedAtUtc,
                RevokedAtUtc = null,
            });

        return token;
    }

    private sealed record CredentialEntry(
        string? Token,
        string? Hint,
        DateTimeOffset? IssuedAtUtc,
        DateTimeOffset? RotatedAtUtc,
        DateTimeOffset? RevokedAtUtc);
}

/// <summary>
/// Non-secret lifecycle view of a module credential. Deliberately contains no token material.
/// </summary>
/// <param name="ModuleId">The module id.</param>
/// <param name="Hint">First characters of the current token, for log correlation.</param>
/// <param name="IssuedAtUtc">When a credential was first issued for the module.</param>
/// <param name="RotatedAtUtc">When the token was last rotated, if ever.</param>
/// <param name="RevokedAtUtc">When the token was revoked, if it currently is.</param>
/// <param name="IsActive">Whether a usable token currently exists.</param>
internal sealed record ModuleCredentialStatus(
    string ModuleId,
    string? Hint,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? RotatedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    bool IsActive);
