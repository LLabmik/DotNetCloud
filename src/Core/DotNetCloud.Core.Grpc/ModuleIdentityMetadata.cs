using Grpc.Core;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Builds the gRPC metadata that identifies a module host to the core process.
/// </summary>
/// <remarks>
/// <para>
/// A module identifier on its own is not proof of identity — any process that can reach the
/// core's loopback gRPC listener could present an arbitrary <c>module-id</c>. The process
/// supervisor therefore mints a unique per-module secret when it launches a module host and
/// hands it over in the <see cref="ModuleSecretEnvironmentVariable"/> environment variable.
/// The module must echo that secret back in <see cref="ModuleSecretHeader"/> on every call to
/// the core, and the core rejects any call whose secret does not match the one it issued.
/// </para>
/// <para>
/// Module hosts should always build their outbound metadata with <see cref="Create"/> so the
/// credential is attached consistently.
/// </para>
/// </remarks>
public static class ModuleIdentityMetadata
{
    /// <summary>The metadata key carrying the module identifier.</summary>
    public const string ModuleIdHeader = "module-id";

    /// <summary>The metadata key carrying the supervisor-issued module secret.</summary>
    public const string ModuleSecretHeader = "module-secret";

    /// <summary>
    /// The environment variable the process supervisor uses to hand a module host its secret.
    /// </summary>
    public const string ModuleSecretEnvironmentVariable = "DOTNETCLOUD_MODULE_SECRET";

    /// <summary>
    /// The metadata key carrying a core-signed assertion of the end user a module is acting for.
    /// </summary>
    public const string UserAssertionHeader = "caller-user-assertion";

    /// <summary>
    /// The environment variable carrying the core's public assertion-signing key, so a module can
    /// verify a user assertion without being able to mint one.
    /// </summary>
    public const string AssertionPublicKeyEnvironmentVariable = "DOTNETCLOUD_ASSERTION_PUBLIC_KEY";

    /// <summary>
    /// Creates the metadata for a core call originating from <paramref name="moduleId"/>,
    /// including the supervisor-issued secret when one is present in the environment.
    /// </summary>
    /// <param name="moduleId">The module identifier reported to the core.</param>
    /// <param name="userAssertion">
    /// A core-signed assertion for the end user being acted for, when the call is user-scoped.
    /// </param>
    /// <returns>The populated gRPC metadata.</returns>
    public static Metadata Create(string moduleId, string? userAssertion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var metadata = new Metadata { { ModuleIdHeader, moduleId } };

        var secret = Environment.GetEnvironmentVariable(ModuleSecretEnvironmentVariable);
        if (!string.IsNullOrEmpty(secret))
        {
            metadata.Add(ModuleSecretHeader, secret);
        }

        if (!string.IsNullOrEmpty(userAssertion))
        {
            metadata.Add(UserAssertionHeader, userAssertion);
        }
        else if (!string.IsNullOrEmpty(ModuleUserAssertionContext.Current))
        {
            // Ambient assertion for the flow currently being served. This is what lets existing
            // capability call sites carry the end-user identity without taking a new parameter.
            metadata.Add(UserAssertionHeader, ModuleUserAssertionContext.Current);
        }

        return metadata;
    }
}
