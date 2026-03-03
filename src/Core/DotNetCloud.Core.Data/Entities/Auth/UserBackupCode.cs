using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Entities.Auth;

/// <summary>
/// Represents a single-use TOTP backup code for two-factor authentication recovery.
/// </summary>
/// <remarks>
/// Backup codes allow users to authenticate when their TOTP authenticator device is unavailable.
/// Each code is stored as a SHA-256 hash and can only be used once.
/// </remarks>
public class UserBackupCode
{
    /// <summary>
    /// Gets or sets the unique identifier for this backup code record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user this backup code belongs to.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the backup code value.
    /// </summary>
    /// <remarks>
    /// The plaintext code is never stored. During verification the supplied code is
    /// hashed and compared against this value.
    /// </remarks>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this code has already been used.
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when this code was generated.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when this code was redeemed.
    /// <see langword="null"/> if the code has not been used.
    /// </summary>
    public DateTime? UsedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the user who owns this backup code.
    /// </summary>
    public virtual ApplicationUser? User { get; set; }
}
