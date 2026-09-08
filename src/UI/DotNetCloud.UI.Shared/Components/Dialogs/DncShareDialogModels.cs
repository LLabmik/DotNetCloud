namespace DotNetCloud.UI.Shared.Components.Dialogs;

/// <summary>
/// A recipient option offered by the share dialog's type-ahead search
/// (a user, team, or — where applicable — a group).
/// </summary>
public sealed class DncShareRecipient
{
    /// <summary>Entity ID (user, team, or group).</summary>
    public Guid Id { get; init; }

    /// <summary>Display name shown in the results list.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Optional secondary line (e.g. email for users, member count for teams).</summary>
    public string? SecondaryText { get; init; }

    /// <summary>Recipient type: "User", "Team", or "Group".</summary>
    public string RecipientType { get; init; } = "User";
}

/// <summary>
/// An existing share displayed in the share dialog's "Current shares" list.
/// </summary>
public sealed class DncShareEntry
{
    /// <summary>Share ID (as stored by the backing module).</summary>
    public Guid ShareId { get; init; }

    /// <summary>Resolved display name of the recipient ("John Doe", team name, or "Public Link").</summary>
    public string RecipientName { get; init; } = string.Empty;

    /// <summary>Recipient type: "User", "Team", "Group", or "PublicLink".</summary>
    public string RecipientType { get; init; } = "User";

    /// <summary>Permission value ("Read", "ReadWrite", or "Full").</summary>
    public string Permission { get; set; } = "Read";

    /// <summary>Full public link URL (PublicLink entries only).</summary>
    public string? LinkUrl { get; init; }

    /// <summary>Whether the public link has a password (PublicLink entries only).</summary>
    public bool HasPassword { get; init; }

    /// <summary>Download count (PublicLink entries only).</summary>
    public int DownloadCount { get; init; }

    /// <summary>Max downloads (PublicLink entries only; null = unlimited).</summary>
    public int? MaxDownloads { get; init; }

    /// <summary>Expiration date (null = never).</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>When the share was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Optional note attached to the share.</summary>
    public string? Note { get; init; }

    /// <summary>Whether this share has already expired.</summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}

/// <summary>
/// A permission level offered in the share dialog's permission dropdown.
/// </summary>
public sealed class DncSharePermissionOption
{
    /// <summary>Permission value sent to the module ("Read", "ReadWrite", "Full").</summary>
    public required string Value { get; init; }

    /// <summary>Human-readable label shown in the dropdown.</summary>
    public required string Label { get; init; }
}

/// <summary>
/// Raised when the user confirms a new share (or bulk share) in the dialog.
/// The parent is responsible for persisting the share against its backend.
/// </summary>
public sealed class DncShareCreatedEventArgs
{
    /// <summary>Recipient type: "User", "Team", or "Group".</summary>
    public string ShareType { get; init; } = "User";

    /// <summary>Target entity ID (user, team, or group).</summary>
    public Guid TargetId { get; init; }

    /// <summary>Display name of the target.</summary>
    public string TargetName { get; init; } = string.Empty;

    /// <summary>Permission value ("Read", "ReadWrite", or "Full").</summary>
    public string Permission { get; init; } = "Read";

    /// <summary>Expiration in days (0 = never).</summary>
    public int ExpirationDays { get; init; }

    /// <summary>Optional note.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// Raised when an existing share is edited (permission, public-link settings).
/// </summary>
public sealed class DncShareUpdatedEventArgs
{
    /// <summary>ID of the share being updated.</summary>
    public Guid ShareId { get; init; }

    /// <summary>New permission level (null = keep current).</summary>
    public string? NewPermission { get; init; }

    /// <summary>New max downloads (null = keep current).</summary>
    public int? NewMaxDownloads { get; init; }

    /// <summary>New expiration in days (0 = never; null = keep current).</summary>
    public int? NewExpirationDays { get; init; }

    /// <summary>New password to set (null = keep current).</summary>
    public string? NewPassword { get; init; }

    /// <summary>Whether to remove the existing password.</summary>
    public bool RemovePassword { get; init; }
}
