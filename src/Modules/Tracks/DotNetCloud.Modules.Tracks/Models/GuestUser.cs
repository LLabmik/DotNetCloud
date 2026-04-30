namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A guest user invited via email to access limited product resources.
/// Guests have restricted permissions and access only what is explicitly shared with them.
/// </summary>
public sealed class GuestUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Email address used for invitation.</summary>
    public required string Email { get; set; }

    /// <summary>Display name for the guest.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The product this guest is associated with.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Who invited this guest.</summary>
    public Guid InvitedByUserId { get; set; }

    /// <summary>Current status of the guest account.</summary>
    public GuestStatus Status { get; set; } = GuestStatus.Pending;

    /// <summary>Token for the invitation acceptance link.</summary>
    public required string InviteToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public ICollection<GuestPermission> Permissions { get; set; } = new List<GuestPermission>();
}

/// <summary>
/// Status of a guest user account.
/// </summary>
public enum GuestStatus
{
    /// <summary>Invited but not yet accepted.</summary>
    Pending = 0,

    /// <summary>Accepted and active.</summary>
    Active = 1,

    /// <summary>Access revoked by product admin.</summary>
    Revoked = 2
}
