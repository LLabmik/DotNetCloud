namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a user account is created.
/// </summary>
/// <remarks>
/// Published by <c>AuthService.RegisterAsync</c> after the identity record and roles exist.
/// Publishing is best-effort: a failure (or a slow subscriber) never fails the registration.
/// The core's default-content dispatcher reacts by provisioning the account immediately.
/// </remarks>
public sealed record UserCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the new user.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The login name.</summary>
    public required string UserName { get; init; }

    /// <summary>The display name shown to other users.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether the account was created as a demo account.</summary>
    public bool IsDemoUser { get; init; }
}
