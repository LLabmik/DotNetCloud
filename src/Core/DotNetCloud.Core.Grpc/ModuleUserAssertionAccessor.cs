using System.Security.Cryptography;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Resolves the end user the core is invoking the current module operation for.
/// </summary>
/// <remarks>
/// <para>
/// This is the module-side counterpart of a core-initiated call that the core makes <i>on behalf of
/// a user</i> (background sync, scheduled delivery). The core mints a short-lived ECDSA assertion
/// naming the user and scoped to the module; <see cref="ModuleUserAssertionMiddleware"/> verifies it
/// on the way in and the token is available on the current flow. A module that needs the identity —
/// rather than just passing it back to the core — asks here instead of trusting a user id in the
/// request payload, which any local caller could invent.
/// </para>
/// <para>
/// The token is re-verified rather than read from request items: the check is cheap (the public key
/// is imported once per process) and it means a host that forgot to add the middleware cannot be
/// talked into naming a user by a payload alone.
/// </para>
/// <para>
/// An operation the module starts on its own timer has no ambient assertion and therefore no user —
/// that is deliberate.
/// </para>
/// </remarks>
public interface IModuleUserAssertionAccessor
{
    /// <summary>
    /// Gets the verified end user for the current flow.
    /// </summary>
    /// <returns>
    /// The user id when the core named a user for this operation; otherwise <see langword="null"/>.
    /// </returns>
    Guid? GetCurrentUserId();
}

/// <summary>
/// Default <see cref="IModuleUserAssertionAccessor"/>, verifying against the core's public key
/// published by the process supervisor.
/// </summary>
internal sealed class ModuleUserAssertionAccessor : IModuleUserAssertionAccessor, IDisposable
{
    private readonly string _audience;
    private readonly ECDsa? _verificationKey;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleUserAssertionAccessor"/> class.
    /// </summary>
    public ModuleUserAssertionAccessor()
    {
        _audience = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? string.Empty;

        // Imported once: key import costs far more than a verification, and this runs per call.
        _verificationKey = ModuleUserAssertions.TryImportPublicKey(
            Environment.GetEnvironmentVariable(
                ModuleIdentityMetadata.AssertionPublicKeyEnvironmentVariable));
    }

    /// <inheritdoc />
    public Guid? GetCurrentUserId()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var token = ModuleUserAssertionContext.Current;

        if (string.IsNullOrEmpty(token)
            || _verificationKey is null
            || !ModuleUserAssertions.TryVerify(
                token, _verificationKey, _audience, DateTimeOffset.UtcNow, out var assertion)
            || assertion is null)
        {
            return null;
        }

        return assertion.UserId;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _verificationKey?.Dispose();
    }
}
