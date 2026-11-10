namespace DotNetCloud.Core.Modules;

/// <summary>
/// The decision states of a module capability request.
/// </summary>
/// <remarks>
/// <para>
/// A manifest declares the capabilities a module needs; the core records an explicit decision for
/// every one that requires an administrator's approval. Modelling "rejected" as a stored state — and
/// not merely as the absence of a grant — is what lets an administrator say "no" once: a rejected
/// capability is not resurrected by the next start-up reconciliation, and the module's detail page can
/// show why it is not running with that capability instead of silently retrying.
/// </para>
/// <para>
/// Only <see cref="Granted"/> allows a capability call. <see cref="Pending"/> and
/// <see cref="Rejected"/> both deny, with different operator-facing messages.
/// </para>
/// </remarks>
public static class ModuleCapabilityGrantStatus
{
    /// <summary>
    /// Declared by the manifest and awaiting an administrator's decision (store-tier modules).
    /// </summary>
    public const string Pending = "Pending";

    /// <summary>The capability is approved and enforceable.</summary>
    public const string Granted = "Granted";

    /// <summary>An administrator declined the request; the module must run without it.</summary>
    public const string Rejected = "Rejected";

    /// <summary>Determines whether a stored state allows the capability.</summary>
    /// <param name="status">The stored state.</param>
    /// <returns><see langword="true"/> only for <see cref="Granted"/>.</returns>
    public static bool IsGranted(string? status) =>
        string.Equals(status, Granted, StringComparison.OrdinalIgnoreCase);
}
