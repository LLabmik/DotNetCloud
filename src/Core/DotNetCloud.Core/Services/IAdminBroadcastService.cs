using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Manages administrator broadcasts — messages delivered as a dismissible modal
/// dialog to every logged-in web (Blazor) user, e.g. a warning about an upcoming
/// server reboot.
/// </summary>
public interface IAdminBroadcastService
{
    /// <summary>
    /// Creates a broadcast and delivers it immediately, or schedules it when
    /// <see cref="CreateAdminBroadcastRequest.ScheduledForUtc"/> is in the future.
    /// </summary>
    /// <param name="request">The broadcast content and delivery options.</param>
    /// <param name="createdByUserId">The administrator creating the broadcast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created broadcast.</returns>
    Task<AdminBroadcastDto> CreateAsync(
        CreateAdminBroadcastRequest request,
        Guid createdByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists broadcasts for the admin history view, newest first.
    /// </summary>
    /// <param name="take">Maximum number of records to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of broadcasts.</returns>
    Task<IReadOnlyList<AdminBroadcastDto>> ListAsync(
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers a scheduled broadcast immediately.
    /// </summary>
    /// <param name="broadcastId">The broadcast to deliver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the broadcast was found and delivered.</returns>
    Task<bool> SendNowAsync(Guid broadcastId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a broadcast. Dismissal records are cascade-deleted, since a
    /// deleted broadcast can never be shown again.
    /// </summary>
    /// <param name="broadcastId">The broadcast to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the broadcast was found and deleted.</returns>
    Task<bool> DeleteAsync(Guid broadcastId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent delivered, non-expired broadcast that the given user has not
    /// dismissed, or <see langword="null"/> when there is nothing to show.
    /// </summary>
    /// <param name="userId">The user to resolve the active broadcast for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active broadcast for the user, or <see langword="null"/>.</returns>
    Task<ActiveAdminBroadcastDto?> GetActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a user dismissed a broadcast so it is never shown to them again.
    /// Safe to call repeatedly for the same user and broadcast.
    /// </summary>
    /// <param name="broadcastId">The dismissed broadcast.</param>
    /// <param name="userId">The user dismissing the broadcast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DismissAsync(Guid broadcastId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers every scheduled broadcast whose time has arrived. Called by the
    /// background scheduler.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of broadcasts delivered.</returns>
    Task<int> PublishPendingAsync(CancellationToken cancellationToken = default);
}
