namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>
/// How a module came to be installed. The tier is derived from provenance at install time and
/// persisted — it must never be inferred from the module id.
/// </summary>
public enum ModuleTrustTier
{
    /// <summary>Shipped with the core; shares the <c>core</c> schema and cannot be uninstalled.</summary>
    Required,

    /// <summary>First-party optional module shipped with the product, sandboxed but auto-granted from its manifest.</summary>
    Bundled,

    /// <summary>Installed from a signed store package or sideload; capabilities require explicit admin approval.</summary>
    Store,
}

/// <summary>How a module stores its own data.</summary>
public enum ModuleDataStoreMode
{
    /// <summary>The module has no database; it persists through module settings and files.</summary>
    None,

    /// <summary>The module owns a dedicated schema and database principal.</summary>
    Isolated,
}

/// <summary>Provisioning state of a module's data store.</summary>
public enum ModuleDataStoreStatus
{
    /// <summary>Provisioning has not completed yet.</summary>
    Pending,

    /// <summary>The store is provisioned and usable.</summary>
    Active,

    /// <summary>Provisioning failed; <c>ModuleDataStore.LastError</c> carries the reason.</summary>
    Error,

    /// <summary>The module was uninstalled with "keep data"; the store is retained for manual cleanup.</summary>
    Orphaned,

    /// <summary>The store is being dropped.</summary>
    Dropping,
}

/// <summary>Sandbox state surfaced to operators.</summary>
public enum ModuleSandboxStatus
{
    /// <summary>Running in the sandboxed model.</summary>
    Active,

    /// <summary>Waiting for data-store provisioning or a prerequisite.</summary>
    PendingProvisioning,

    /// <summary>Provisioning or startup failed.</summary>
    Error,

    /// <summary>A resource quota was exceeded and the module was suspended.</summary>
    QuotaExceeded,

    /// <summary>Running under the temporary <c>Modules:AllowLegacyAccess</c> override.</summary>
    LegacyOverride,
}

/// <summary>Lifecycle state of a user-erasure request.</summary>
public enum UserErasureRequestStatus
{
    /// <summary>At least one module has not acknowledged the erasure.</summary>
    Pending,

    /// <summary>Every reachable module acknowledged the erasure.</summary>
    Completed,

    /// <summary>The request exhausted its retry window.</summary>
    Failed,

    /// <summary>An administrator force-completed the request.</summary>
    Overridden,
}

/// <summary>Per-module state within a user-erasure request.</summary>
public enum ModuleUserErasureStatus
{
    /// <summary>The module has not been reached yet.</summary>
    Pending,

    /// <summary>The module confirmed the erasure.</summary>
    Acknowledged,

    /// <summary>The module reported a failure; <c>LastError</c> carries the reason.</summary>
    Failed,
}
