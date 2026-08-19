namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Result of an OpenIddict signing/encryption key rotation (SOC 2 CC6 / C1).
/// </summary>
public sealed class OidcKeyRotationResult
{
    /// <summary>
    /// The KeyId of the newly generated signing key.
    /// </summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>
    /// The KeyId of the newly generated encryption key.
    /// </summary>
    public string EncryptionKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Path to the backup copy of the previous keys (empty if none existed).
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// When the rotation was performed (UTC).
    /// </summary>
    public DateTime RotatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Operator note — the new keys take effect on the next server restart.
    /// </summary>
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// Status of OpenIddict key rotation, used by the admin UI to show the
/// "restart to activate keys" banner (SOC 2 CC6 / C1).
/// </summary>
public sealed class OidcKeyRotationStatus
{
    /// <summary>
    /// Whether a key rotation has occurred since the last server start
    /// (i.e., a restart is required to activate the newest keys).
    /// </summary>
    public bool RestartPending { get; set; }

    /// <summary>
    /// When the pending rotation occurred (UTC), if any.
    /// </summary>
    public DateTime? RotatedAtUtc { get; set; }

    /// <summary>
    /// The KeyId of the newly generated signing key, if a rotation is pending.
    /// </summary>
    public string? SigningKeyId { get; set; }
}
