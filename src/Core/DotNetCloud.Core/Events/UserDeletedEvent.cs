namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a user account is permanently deleted from the system.
/// Subscribers should clean up all data associated with the deleted user.
/// </summary>
/// <remarks>
/// Published by <c>UserManagementService.DeleteUserAsync</c> after the user
/// record is successfully removed from the identity store.
/// </remarks>
public sealed record UserDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the deleted user.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// When the user was deleted (UTC).
    /// </summary>
    public required DateTime DeletedAt { get; init; }
}
