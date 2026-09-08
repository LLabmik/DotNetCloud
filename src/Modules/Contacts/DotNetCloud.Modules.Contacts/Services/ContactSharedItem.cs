using DotNetCloud.Modules.Contacts.Models;

namespace DotNetCloud.Modules.Contacts.Services;

/// <summary>
/// A contact shared with the caller (direct user share or a share targeting a team the caller
/// belongs to). Returned by the Contacts host "shared with me" endpoint and consumed by the
/// Files shared-with-me aggregator and by module UIs that need to surface shared contacts.
/// </summary>
public sealed record ContactSharedItem
{
    /// <summary>The unique identifier of the shared contact.</summary>
    public Guid ContactId { get; init; }

    /// <summary>The contact's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>The user who created the share (the contact owner).</summary>
    public Guid SharedByUserId { get; init; }

    /// <summary>User this is shared with (null for team shares).</summary>
    public Guid? SharedWithUserId { get; init; }

    /// <summary>Team this is shared with (null for user shares).</summary>
    public Guid? SharedWithTeamId { get; init; }

    /// <summary>Permission level granted by the share.</summary>
    public ContactSharePermission Permission { get; init; } = ContactSharePermission.ReadOnly;

    /// <summary>When the share was created (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the share expires (null for permanent).</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>When the contact was last modified (UTC) — display/ordering fallback.</summary>
    public DateTime UpdatedAt { get; init; }
}
