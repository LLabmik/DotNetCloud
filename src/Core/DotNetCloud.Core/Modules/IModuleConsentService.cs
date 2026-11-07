namespace DotNetCloud.Core.Modules;

/// <summary>
/// One module's consent record as shown on a user's "Connected apps" page.
/// </summary>
/// <param name="ModuleId">The module id.</param>
/// <param name="ModuleName">The module's display name.</param>
/// <param name="Scopes">The scopes the user has approved, in catalog order.</param>
/// <param name="GrantedAtUtc">When the most recent scope was approved.</param>
/// <param name="IsModuleInstalled">Whether the module is still installed.</param>
public sealed record ModuleConsentSummary(
    string ModuleId,
    string ModuleName,
    IReadOnlyList<string> Scopes,
    DateTime? GrantedAtUtc,
    bool IsModuleInstalled);

/// <summary>
/// One user's consent record for a module, as shown to an administrator on the module detail page.
/// </summary>
/// <param name="UserId">The user id (pseudonymous; the admin UI resolves the display name).</param>
/// <param name="Scopes">The scopes the user has approved.</param>
/// <param name="GrantedAtUtc">When the most recent scope was approved.</param>
/// <param name="GrantedByAdmin"><see langword="true"/> when an administrator granted on the user's behalf.</param>
public sealed record ModuleConsentUserSummary(
    Guid UserId,
    IReadOnlyList<string> Scopes,
    DateTime? GrantedAtUtc,
    bool GrantedByAdmin);

/// <summary>
/// Aggregate consent figures for one module, shown on the admin module detail page.
/// </summary>
/// <param name="ConsentingUsers">How many distinct users have consented to at least one scope.</param>
/// <param name="ScopeCounts">Per-scope consented user counts.</param>
public sealed record ModuleConsentOverview(
    int ConsentingUsers,
    IReadOnlyList<ModuleScopeConsentCount> ScopeCounts);

/// <summary>How many users consented to one scope of a module.</summary>
/// <param name="Scope">The scope name.</param>
/// <param name="UserCount">The number of users holding an active consent for it.</param>
public sealed record ModuleScopeConsentCount(string Scope, int UserCount);

/// <summary>
/// Stores and evaluates per-user consent for module scopes.
/// </summary>
/// <remarks>
/// <para>
/// Consent is stored per (module, user, scope). A module call that reaches for a user's personal
/// data checks both the admin capability grant (enforced elsewhere) and a row here; the missing
/// rows travel back to the module as <c>MODULE_CONSENT_REQUIRED</c> so it can request exactly the
/// scopes it still needs.
/// </para>
/// <para>
/// Implementations cache grants briefly for the hot path and invalidate on every write, so a
/// revocation stops the next call rather than the next cache expiry.
/// </para>
/// </remarks>
public interface IModuleConsentService
{
    /// <summary>Gets the scopes a user has actively consented to for a module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The granted scopes; empty when the user has consented to nothing.</returns>
    Task<IReadOnlyList<string>> GetGrantedScopesAsync(
        string moduleId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the subset of <paramref name="requiredScopes"/> the user has not consented to.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="requiredScopes">The scopes the call needs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The missing scopes, in input order.</returns>
    Task<IReadOnlyList<string>> GetMissingScopesAsync(
        string moduleId,
        Guid userId,
        IReadOnlyCollection<string> requiredScopes,
        CancellationToken cancellationToken = default);

    /// <summary>Determines whether a user has consented to one scope.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when an active consent row exists.</returns>
    Task<bool> HasConsentAsync(
        string moduleId,
        Guid userId,
        string scope,
        CancellationToken cancellationToken = default);

    /// <summary>Grants the given scopes to a module for a user.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="scopes">The scopes to grant.</param>
    /// <param name="grantedByUserId">The administrator granting on the user's behalf, or <see langword="null"/>.</param>
    /// <param name="expiresAtUtc">An optional expiry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows created or refreshed.</returns>
    Task<int> GrantAsync(
        string moduleId,
        Guid userId,
        IReadOnlyCollection<string> scopes,
        Guid? grantedByUserId = null,
        DateTime? expiresAtUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes one scope.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="scope">The scope to revoke.</param>
    /// <param name="revokedByUserId">The administrator acting on the user's behalf, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    Task<int> RevokeAsync(
        string moduleId,
        Guid userId,
        string scope,
        Guid? revokedByUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes every scope a user granted to a module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="revokedByUserId">The administrator acting on the user's behalf, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    Task<int> RevokeAllAsync(
        string moduleId,
        Guid userId,
        Guid? revokedByUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes every consent row for a module (uninstall).</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="revokedByUserId">The administrator performing the removal, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    Task<int> RevokeAllForModuleAsync(
        string moduleId,
        Guid? revokedByUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes every consent row for a user (account erasure).</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    Task<int> DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Lists the modules a user has consented to ("Connected apps").</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One entry per module, ordered by module name.</returns>
    Task<IReadOnlyList<ModuleConsentSummary>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists the users who consented to a module (admin view).</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One entry per consenting user.</returns>
    Task<IReadOnlyList<ModuleConsentUserSummary>> ListForModuleAsync(
        string moduleId,
        CancellationToken cancellationToken = default);

    /// <summary>Builds the aggregate consent overview for a module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-scope user counts and the total number of consenting users.</returns>
    Task<ModuleConsentOverview> GetOverviewAsync(
        string moduleId,
        CancellationToken cancellationToken = default);

    /// <summary>Drops any cached grant set for a (module, user) pair.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="userId">The user id.</param>
    void InvalidateCache(string moduleId, Guid userId);
}
