namespace DotNetCloud.Core.Modules;

/// <summary>
/// The details of one user-erasure call from the core.
/// </summary>
/// <param name="RequestId">
/// Stable id of the erasure request. A retry of the same request carries the same id, so an
/// implementation may use it as an idempotency key.
/// </param>
/// <param name="UserId">The deleted user.</param>
/// <param name="Scopes">The consent scopes the module held for the user (advisory).</param>
public sealed record ModuleUserErasureContext(
    Guid RequestId,
    Guid UserId,
    IReadOnlyCollection<string> Scopes);

/// <summary>
/// The outcome of a module's user-erasure attempt.
/// </summary>
/// <param name="Acknowledged">
/// <see langword="true"/> when the module erased everything it holds for the user.
/// </param>
/// <param name="DeletedCounts">Rows/objects deleted, keyed by scope (or <c>all</c>).</param>
/// <param name="Error">Why the erasure could not complete, when it did not.</param>
/// <param name="NotApplicable"><see langword="true"/> when the module holds no per-user data.</param>
public sealed record ModuleUserErasureResult(
    bool Acknowledged,
    IReadOnlyDictionary<string, int> DeletedCounts,
    string? Error = null,
    bool NotApplicable = false)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="deletedCounts">Optional per-scope deletion counts.</param>
    /// <returns>The result.</returns>
    public static ModuleUserErasureResult Success(IReadOnlyDictionary<string, int>? deletedCounts = null) =>
        new(true, deletedCounts ?? new Dictionary<string, int>());

    /// <summary>Creates a result for a module that holds no per-user data.</summary>
    /// <returns>The result.</returns>
    public static ModuleUserErasureResult NotHeld() =>
        new(true, new Dictionary<string, int>(), NotApplicable: true);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">Why the erasure did not complete.</param>
    /// <returns>The result.</returns>
    public static ModuleUserErasureResult Failed(string error) =>
        new(false, new Dictionary<string, int>(), error);
}

/// <summary>
/// Implemented by a module host that stores per-user data.
/// </summary>
/// <remarks>
/// <para>
/// Deleting an account must delete the user's data <em>everywhere</em>, including inside module
/// schemas and module-owned files (F12). The core calls <c>EraseUserData</c> on every installed
/// module, records the acknowledgement per module, and retries for 72 hours; an account deletion
/// never reports success while a module row is outstanding.
/// </para>
/// <para>
/// A host registers its handler with <c>AddModuleUserErasureHandler&lt;T&gt;()</c>. A module that
/// holds no per-user data registers nothing, and answers <c>not_applicable</c> automatically.
/// </para>
/// </remarks>
public interface IModuleUserErasureHandler
{
    /// <summary>Erases everything the module holds for the user.</summary>
    /// <param name="context">The erasure request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<ModuleUserErasureResult> EraseAsync(
        ModuleUserErasureContext context,
        CancellationToken cancellationToken = default);
}
