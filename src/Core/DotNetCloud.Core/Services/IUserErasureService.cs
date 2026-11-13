using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Orchestrates account-erasure fan-out to every installed module.
/// </summary>
/// <remarks>
/// <para>
/// Deleting an account must delete the user's data everywhere (F12). Core user data disappears when
/// the account row is removed; this service is what makes the same happen inside module schemas and
/// module-owned files. It is deliberately <b>retrying and asynchronous</b>: a module may be stopped,
/// restarting or temporarily unreachable when the account is deleted, and the erasure must not be
/// lost because of that.
/// </para>
/// <para>
/// A deletion is never reported as fully complete while a module row is outstanding: the request
/// stays <c>Pending</c>, retries for 72 hours, then surfaces as <c>Failed</c> to administrators, who
/// can force-complete it (audited) if the module is gone for good.
/// </para>
/// </remarks>
public interface IUserErasureService
{
    /// <summary>
    /// Creates (or returns the existing outstanding) erasure request for a deleted user.
    /// </summary>
    /// <param name="userId">The deleted user.</param>
    /// <param name="requestedByUserId">The administrator, or <see langword="null"/> when the account deleted itself.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The request, including one row per erasure-capable module.</returns>
    Task<UserErasureRequestDto> RequestErasureAsync(
        Guid userId,
        Guid? requestedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one erasure request.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The request, or <see langword="null"/> when it does not exist.</returns>
    Task<UserErasureRequestDto?> GetAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Lists erasure requests, newest first.</summary>
    /// <param name="includeCompleted">Whether terminal requests are included.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requests.</returns>
    Task<IReadOnlyList<UserErasureRequestDto>> ListAsync(
        bool includeCompleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>Delivers every outstanding erasure to its module and records the outcome.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of module rows that reached a terminal state in this pass.</returns>
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Force-completes a request that exhausted its retry window. Audited.
    /// </summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="adminUserId">The administrator overriding it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the request existed and was overridden.</returns>
    Task<bool> OverrideAsync(
        Guid requestId,
        Guid adminUserId,
        CancellationToken cancellationToken = default);
}
