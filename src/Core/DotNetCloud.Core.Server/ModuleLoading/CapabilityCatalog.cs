using DotNetCloud.Core.Capabilities;

namespace DotNetCloud.Core.Server.ModuleLoading;

/// <summary>
/// Single source of truth for the capability tier model.
/// </summary>
/// <remarks>
/// <para>
/// The catalog is used by the gRPC enforcement interceptor (to decide whether a call needs a
/// persisted grant) and by the process supervisor (to decide which manifest-declared
/// capabilities are provisioned as <c>ModuleCapabilityGrants</c> rows).
/// </para>
/// <para>
/// Capabilities that are absent from every tier are treated as <em>unknown</em>: calls mapped to
/// them are denied unless the calling module holds a grant. Add a capability to a tier here when
/// it becomes part of the module API surface.
/// </para>
/// </remarks>
internal static class CapabilityCatalog
{
    /// <summary>Capabilities every authenticated module may use without a persisted grant.</summary>
    private static readonly HashSet<string> PublicCapabilities =
    [
        nameof(IUserDirectory),
        nameof(ICurrentUserContext),
        nameof(INotificationService),
        nameof(IEventBus)
    ];

    /// <summary>
    /// Capabilities that require an explicit <c>ModuleCapabilityGrant</c> row. Restricted
    /// capabilities expose data or platform side effects beyond the module's own sandbox.
    /// </summary>
    private static readonly HashSet<string> RestrictedCapabilities =
    [
        nameof(IStorageProvider),
        nameof(IModuleSettings),
        nameof(ITeamDirectory),
        nameof(IOrganizationDirectory),

        // Module-facing capability RPCs whose grant rows were previously absent from the
        // catalog, which made the calls unmapped (and therefore unenforced) in the interceptor.
        nameof(IAuditLogger),
        nameof(ISearchProvider),
        nameof(IRealtimeBroadcaster)
    ];

    /// <summary>Capabilities that require an explicit, privileged grant.</summary>
    private static readonly HashSet<string> PrivilegedCapabilities =
    [
        nameof(IUserManager),
        nameof(IBackupProvider),
        nameof(IAdminSharedFolderCleanup)
    ];

    /// <summary>Interfaces a module may never be granted.</summary>
    private static readonly HashSet<string> ForbiddenCapabilities =
    [
        "CoreDbContext",
        "IConfiguration",
        "IServiceProvider",
        "PasswordHasher",
        "SigningKey"
    ];

    /// <summary>
    /// Gets the tier for a capability name.
    /// </summary>
    /// <param name="capabilityName">The capability interface name.</param>
    /// <returns>The capability tier, or <see langword="null"/> when the capability is unknown.</returns>
    public static CapabilityTier? GetCapabilityTier(string? capabilityName)
    {
        if (string.IsNullOrWhiteSpace(capabilityName))
        {
            return null;
        }

        if (PublicCapabilities.Contains(capabilityName))
        {
            return CapabilityTier.Public;
        }

        if (RestrictedCapabilities.Contains(capabilityName))
        {
            return CapabilityTier.Restricted;
        }

        if (PrivilegedCapabilities.Contains(capabilityName))
        {
            return CapabilityTier.Privileged;
        }

        if (ForbiddenCapabilities.Contains(capabilityName))
        {
            return CapabilityTier.Forbidden;
        }

        return null;
    }

    /// <summary>
    /// Determines whether a capability is governed by <c>ModuleCapabilityGrants</c>.
    /// </summary>
    /// <param name="capabilityName">The capability interface name.</param>
    /// <returns>
    /// <see langword="true"/> when the capability is restricted or privileged; otherwise
    /// <see langword="false"/> (public, forbidden or unknown capabilities are not grant-governed).
    /// </returns>
    public static bool RequiresGrant(string? capabilityName) =>
        GetCapabilityTier(capabilityName) is CapabilityTier.Restricted or CapabilityTier.Privileged;
}
