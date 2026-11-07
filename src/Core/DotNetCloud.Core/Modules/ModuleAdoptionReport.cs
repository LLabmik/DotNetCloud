namespace DotNetCloud.Core.Modules;

/// <summary>The adoption decision for one installed module.</summary>
public enum ModuleAdoptionOutcome
{
    /// <summary>The module's existing schema can be adopted now.</summary>
    Adoptable,

    /// <summary>The schema was adopted successfully.</summary>
    Adopted,

    /// <summary>The module already has a provisioned, active data store.</summary>
    AlreadyProvisioned,

    /// <summary>No adoption is needed (required module, or <c>dataStore: "none"</c>).</summary>
    Skipped,

    /// <summary>Provisioning prerequisites are missing; the module will be held with an error.</summary>
    Blocked,

    /// <summary>Adoption was attempted and failed.</summary>
    Failed,

    /// <summary>The bundled-module legacy-access override was applied instead of adoption.</summary>
    LegacyOverride,
}

/// <summary>One row of the adoption preflight/result report.</summary>
/// <param name="ModuleId">The module identifier.</param>
/// <param name="TrustTier">The module trust tier.</param>
/// <param name="DataStoreMode">The manifest data-store mode.</param>
/// <param name="Outcome">The adoption decision or result.</param>
/// <param name="Reason">An operator-facing explanation, when one applies.</param>
public sealed record ModuleAdoptionEntry(
    string ModuleId,
    string TrustTier,
    string DataStoreMode,
    ModuleAdoptionOutcome Outcome,
    string? Reason)
{
    /// <summary>Gets whether this entry requires operator action.</summary>
    public bool NeedsAttention => Outcome is ModuleAdoptionOutcome.Blocked or ModuleAdoptionOutcome.Failed;
}

/// <summary>The adoption preflight/result report for every installed module.</summary>
/// <param name="Entries">One entry per installed module, ordered by module id.</param>
public sealed record ModuleAdoptionReport(IReadOnlyList<ModuleAdoptionEntry> Entries)
{
    /// <summary>Gets whether any module is blocked or failed.</summary>
    public bool HasBlockingProblems => Entries.Any(e => e.NeedsAttention);
}
