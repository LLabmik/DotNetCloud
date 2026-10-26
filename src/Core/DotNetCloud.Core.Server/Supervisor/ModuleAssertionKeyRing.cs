using System.Security.Cryptography;
using DotNetCloud.Core.Grpc;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Holds the core's assertion-signing key and mints end-user assertions for module calls.
/// </summary>
/// <remarks>
/// <para>
/// Only the core has the private half. Modules receive the public half at launch (through
/// <see cref="ModuleIdentityMetadata.AssertionPublicKeyEnvironmentVariable"/>) and can therefore
/// verify an assertion but never produce one — which is what makes "the module says it is acting
/// for user X" a claim the core can actually trust.
/// </para>
/// <para>
/// The key pair lives for the lifetime of the core process. Assertions are short-lived, so a core
/// restart simply invalidates the outstanding ones and every module picks up the new public key
/// when it is relaunched.
/// </para>
/// </remarks>
internal sealed class ModuleAssertionKeyRing : IDisposable
{
    /// <summary>How long a freshly minted assertion stays valid.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(60);

    /// <summary>The operation value used when a call is not further narrowed.</summary>
    public const string DefaultOperation = "act";

    private readonly ECDsa _key;
    private bool _disposed;

    public ModuleAssertionKeyRing()
    {
        _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublicKeyBase64 = Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());
    }

    /// <summary>
    /// Gets the public key, in SubjectPublicKeyInfo form, base64 encoded.
    /// </summary>
    public string PublicKeyBase64 { get; }

    /// <summary>
    /// Mints a signed assertion for <paramref name="userId"/> scoped to <paramref name="audience"/>.
    /// </summary>
    /// <param name="userId">The end-user identifier.</param>
    /// <param name="audience">The module the assertion is valid for.</param>
    /// <param name="operation">Optional coarse operation scope.</param>
    /// <param name="lifetime">Optional lifetime override.</param>
    /// <returns>The signed compact token.</returns>
    public string Mint(
        Guid userId,
        string audience,
        string operation = DefaultOperation,
        TimeSpan? lifetime = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An assertion cannot name an empty user id.", nameof(userId));
        }

        var assertion = new ModuleUserAssertion
        {
            UserId = userId,
            Audience = audience,
            Operation = operation,
            ExpiresAtUnixSeconds = DateTimeOffset.UtcNow
                .Add(lifetime ?? DefaultLifetime)
                .ToUnixTimeSeconds(),
            Id = Guid.CreateVersion7(),
        };

        return ModuleUserAssertions.Sign(assertion, _key);
    }

    /// <summary>
    /// Verifies an assertion token against this key ring's public key.
    /// </summary>
    /// <param name="token">The compact token presented by the module.</param>
    /// <param name="expectedAudience">The module id the assertion must be scoped to.</param>
    /// <param name="assertion">The verified payload when verification succeeds.</param>
    /// <returns><see langword="true"/> when the token is genuine, in scope and unexpired.</returns>
    public bool TryValidate(string? token, string expectedAudience, out ModuleUserAssertion? assertion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The same key object both signs and verifies, so validation never re-imports key material.
        return ModuleUserAssertions.TryVerify(
            token,
            _key,
            expectedAudience,
            DateTimeOffset.UtcNow,
            out assertion);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _key.Dispose();
        _disposed = true;
    }
}
