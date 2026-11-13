namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Single source of truth for the capability tier model and the RPC → capability mapping of the
/// module-facing gRPC surfaces.
/// </summary>
/// <remarks>
/// <para>
/// Lives in <c>DotNetCloud.Core</c> (not the server) so the module API implementation, the access
/// evaluator, manifest grant provisioning and the module SDK can all read the same table. A copy in
/// the SDK would drift from the core, and drift is exactly how an RPC ends up enforced by nothing.
/// </para>
/// <para>
/// Mapping is keyed by the **fully-qualified** gRPC method path
/// (<c>/package.Service/Method</c>). Keying by the simple method name is ambiguous as soon as two
/// services expose the same name with different capabilities, which the canonical
/// <c>module_api</c> service does.
/// </para>
/// <para>
/// Capabilities absent from every tier are <em>unknown</em>: a call mapped to one is denied unless
/// the module holds a grant. Add a capability to a tier here when it becomes part of the module API
/// surface.
/// </para>
/// </remarks>
public static class CapabilityCatalog
{
    /// <summary>Fully-qualified service name of the legacy capability surface.</summary>
    public const string CoreCapabilitiesService = "dotnetcloud.capabilities.CoreCapabilities";

    /// <summary>Fully-qualified service name of the canonical module API surface.</summary>
    public const string ModuleApiService = "dotnetcloud.moduleapi.v1.ModuleApi";

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

        // The acting user's real files. A grant permits the call; per-user consent to
        // files:read / files:write decides whose files, and whether at all.
        nameof(IFileDirectory),
        nameof(IFileWriter),

        // Module-facing capability RPCs whose grants were previously absent from the catalog,
        // which left the calls unmapped (and therefore unenforced).
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
    /// Maps a fully-qualified gRPC method path to the capability it exercises.
    /// </summary>
    /// <remarks>
    /// Group/team lookups all enforce <see cref="ITeamDirectory"/>: that is the directory capability
    /// modules declare and the only grant the hosts hold when they resolve shared-folder or team
    /// membership. Unifying <c>IGroupDirectory</c> and <c>ITeamDirectory</c> behind one catalog entry
    /// is deliberate.
    /// </remarks>
    private static readonly Dictionary<string, string> MethodCapabilities = new(StringComparer.Ordinal)
    {
        [Rpc(CoreCapabilitiesService, "GetUser")] = nameof(IUserDirectory),
        [Rpc(CoreCapabilitiesService, "SearchUsers")] = nameof(IUserDirectory),
        [Rpc(CoreCapabilitiesService, "GetCurrentUser")] = nameof(ICurrentUserContext),
        [Rpc(CoreCapabilitiesService, "SendNotification")] = nameof(INotificationService),
        [Rpc(CoreCapabilitiesService, "PublishEvent")] = nameof(IEventBus),
        [Rpc(CoreCapabilitiesService, "GetSetting")] = nameof(IModuleSettings),
        [Rpc(CoreCapabilitiesService, "SetSetting")] = nameof(IModuleSettings),
        [Rpc(CoreCapabilitiesService, "GetGroup")] = nameof(ITeamDirectory),
        [Rpc(CoreCapabilitiesService, "GetGroupsForUser")] = nameof(ITeamDirectory),
        [Rpc(CoreCapabilitiesService, "GetTeam")] = nameof(ITeamDirectory),
        [Rpc(CoreCapabilitiesService, "GetTeamsForUser")] = nameof(ITeamDirectory),
        [Rpc(CoreCapabilitiesService, "GetUserOrganizations")] = nameof(IOrganizationDirectory),
        [Rpc(CoreCapabilitiesService, "CleanupAdminSharedFolder")] = nameof(IAdminSharedFolderCleanup),

        // Re-tiered away from Public INotificationService: broadcasting a platform-branded
        // real-time event to any group is not the same power as sending a notification.
        [Rpc(CoreCapabilitiesService, "BroadcastRealtimeEvent")] = nameof(IRealtimeBroadcaster),
        [Rpc(CoreCapabilitiesService, "LogAudit")] = nameof(IAuditLogger),
        [Rpc(CoreCapabilitiesService, "SubmitSearchIndex")] = nameof(ISearchProvider),

        // Canonical module API (module_api.proto).
        [Rpc(ModuleApiService, "GetUser")] = nameof(IUserDirectory),
        [Rpc(ModuleApiService, "SearchUsers")] = nameof(IUserDirectory),
        [Rpc(ModuleApiService, "GetCurrentUserContext")] = nameof(ICurrentUserContext),
        [Rpc(ModuleApiService, "SendNotification")] = nameof(INotificationService),
        [Rpc(ModuleApiService, "PublishEvent")] = nameof(IEventBus),
        [Rpc(ModuleApiService, "GetSetting")] = nameof(IModuleSettings),
        [Rpc(ModuleApiService, "SetSetting")] = nameof(IModuleSettings),
        [Rpc(ModuleApiService, "GetGroup")] = nameof(ITeamDirectory),
        [Rpc(ModuleApiService, "GetGroupsForUser")] = nameof(ITeamDirectory),
        [Rpc(ModuleApiService, "GetTeam")] = nameof(ITeamDirectory),
        [Rpc(ModuleApiService, "GetTeamsForUser")] = nameof(ITeamDirectory),
        [Rpc(ModuleApiService, "GetUserOrganizations")] = nameof(IOrganizationDirectory),
        [Rpc(ModuleApiService, "BroadcastRealtimeEvent")] = nameof(IRealtimeBroadcaster),
        [Rpc(ModuleApiService, "LogAudit")] = nameof(IAuditLogger),
        [Rpc(ModuleApiService, "SubmitSearchIndex")] = nameof(ISearchProvider),

        // Files: reading and writing the acting user's files are separate capabilities so a module
        // that only exports can be granted read-only access. Both are brokered to the Files module
        // by the core; the module never reaches it directly.
        [Rpc(ModuleApiService, "ListFiles")] = nameof(IFileDirectory),
        [Rpc(ModuleApiService, "DownloadFile")] = nameof(IFileDirectory),
        [Rpc(ModuleApiService, "UploadFile")] = nameof(IFileWriter),
    };

    /// <summary>
    /// Module-facing RPCs that need no capability because they only report or read state belonging
    /// to the authenticated module / acting user. Every entry is an explicit decision: an RPC that
    /// is neither mapped nor listed here is denied (see <see cref="IsKnownRpc"/>).
    /// </summary>
    private static readonly HashSet<string> SelfServiceMethods = new(StringComparer.Ordinal)
    {
        // Module reports its own health; the core records it for this module only.
        Rpc(ModuleApiService, "ReportHealth"),

        // Consent queries are about the calling module's own (module, user) grants, so their answer
        // is not another module's data. Rendering/consuming the data still needs the capability and
        // the consent check at the data RPC.
        Rpc(ModuleApiService, "CheckUserConsent"),
        Rpc(ModuleApiService, "GetGrantedScopes"),
    };

    /// <summary>
    /// Gets the fully-qualified gRPC method path used as a catalog key.
    /// </summary>
    /// <param name="service">Fully-qualified service name (for example <c>dotnetcloud.moduleapi.v1.ModuleApi</c>).</param>
    /// <param name="method">The RPC name.</param>
    /// <returns>The normalized path (<c>/service/method</c>).</returns>
    public static string Rpc(string service, string method) =>
        $"/{service.Trim('/')}/{method}";

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

    /// <summary>
    /// Resolves the capability a gRPC method exercises.
    /// </summary>
    /// <param name="rpcMethod">The fully-qualified gRPC method path.</param>
    /// <returns>
    /// The capability name; <see langword="null"/> for a self-service RPC or one outside the
    /// module-facing services.
    /// </returns>
    public static string? ResolveCapability(string? rpcMethod) =>
        Normalize(rpcMethod) is { } method && MethodCapabilities.TryGetValue(method, out var capability)
            ? capability
            : null;

    /// <summary>
    /// Determines whether a method belongs to a module-facing service and therefore must be mapped.
    /// </summary>
    /// <param name="rpcMethod">The fully-qualified gRPC method path.</param>
    /// <returns><see langword="true"/> for methods on <see cref="CoreCapabilitiesService"/> or <see cref="ModuleApiService"/>.</returns>
    public static bool IsModuleFacingRpc(string? rpcMethod) =>
        Normalize(rpcMethod) is { } method
        && (method.StartsWith($"/{CoreCapabilitiesService}/", StringComparison.Ordinal)
            || method.StartsWith($"/{ModuleApiService}/", StringComparison.Ordinal));

    /// <summary>
    /// Determines whether a module-facing RPC is allowed without a capability grant.
    /// </summary>
    /// <param name="rpcMethod">The fully-qualified gRPC method path.</param>
    /// <returns><see langword="true"/> when the method is on the explicit self-service allow-list.</returns>
    public static bool IsSelfServiceRpc(string? rpcMethod) =>
        Normalize(rpcMethod) is { } method && SelfServiceMethods.Contains(method);

    /// <summary>
    /// Determines whether a module-facing RPC has an explicit enforcement decision.
    /// </summary>
    /// <param name="rpcMethod">The fully-qualified gRPC method path.</param>
    /// <returns>
    /// <see langword="true"/> when the method is mapped to a capability or allow-listed as
    /// self-service. Anything else on a module-facing service is denied.
    /// </returns>
    public static bool IsKnownRpc(string? rpcMethod) =>
        ResolveCapability(rpcMethod) is not null || IsSelfServiceRpc(rpcMethod);

    /// <summary>Gets every module-facing RPC with an explicit enforcement decision.</summary>
    /// <returns>The mapped and allow-listed method paths.</returns>
    public static IReadOnlyCollection<string> GetKnownRpcs() =>
        MethodCapabilities.Keys.Concat(SelfServiceMethods).ToArray();

    /// <summary>Normalizes a method path to the catalog key form.</summary>
    private static string? Normalize(string? rpcMethod)
    {
        if (string.IsNullOrWhiteSpace(rpcMethod))
        {
            return null;
        }

        var trimmed = rpcMethod.Trim();

        // ServerCallContext.Method is already "/service/method"; accept a missing leading slash.
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }
}
