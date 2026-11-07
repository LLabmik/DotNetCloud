using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>
/// A user's consent for a single (module, scope) pair.
/// </summary>
/// <remarks>
/// <para>
/// Consent is the second half of the module authorization model. The admin's capability grant says
/// "this module may call the storage API at all"; a consent row says "this user allows this module
/// to read <em>their</em> files". Without a row the module call fails with
/// <c>MODULE_CONSENT_REQUIRED</c> and the user is prompted.
/// </para>
/// <para>
/// Consent is per (module, user, scope), enforced by a unique index. Rows survive module updates —
/// a new version that adds a scope simply does not have a row for it yet, so the user is re-prompted
/// the first time the module needs it and keeps the access the user already approved.
/// </para>
/// <para>
/// Rows are deleted when the user is erased and when the module is uninstalled. Revocation stops
/// future calls; it cannot take back data the module already holds — store review and the audit log
/// are the compensating controls.
/// </para>
/// </remarks>
public class ModuleUserConsent
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The module the user granted access to (e.g. <c>vendor.receipts</c>).</summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>The user who granted the consent.</summary>
    public Guid UserId { get; set; }

    /// <summary>The granted scope, for example <c>files:read</c>.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>When the consent was granted (UTC).</summary>
    public DateTime GrantedAtUtc { get; set; }

    /// <summary>When the consent stops being valid; <see langword="null"/> means it does not expire.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>
    /// The administrator who granted the consent on the user's behalf, or <see langword="null"/>
    /// when the user granted it themselves.
    /// </summary>
    public Guid? GrantedByUserId { get; set; }

    /// <summary>Navigation to the installed module.</summary>
    public virtual InstalledModule? Module { get; set; }

    /// <summary>Navigation to the consenting user.</summary>
    public virtual ApplicationUser? User { get; set; }

    /// <summary>Navigation to the administrator who granted on the user's behalf, if any.</summary>
    public virtual ApplicationUser? GrantedByUser { get; set; }

    /// <summary>Determines whether the consent is still valid at the given instant.</summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns><see langword="true"/> when the consent has not expired.</returns>
    public bool IsActiveAt(DateTime utcNow) => ExpiresAtUtc is null || ExpiresAtUtc > utcNow;
}
