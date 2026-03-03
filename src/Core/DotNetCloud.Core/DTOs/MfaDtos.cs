namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Response containing TOTP authenticator setup information.
/// </summary>
public sealed class TotpSetupResponse
{
    /// <summary>
    /// Gets or sets the Base32-encoded shared secret key for the authenticator app.
    /// </summary>
    public string SharedKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the <c>otpauth://</c> URI that encodes the key for QR code generation.
    /// </summary>
    public string QrCodeUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of unused backup codes remaining for this account.
    /// </summary>
    public int RecoveryCodesRemaining { get; set; }
}

/// <summary>
/// Request to verify a TOTP code supplied by the user.
/// </summary>
public sealed class TotpVerifyRequest
{
    /// <summary>
    /// Gets or sets the 6-digit TOTP code from the authenticator app.
    /// </summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Response containing a set of newly generated backup codes.
/// </summary>
public sealed class BackupCodesResponse
{
    /// <summary>
    /// Gets or sets the list of plaintext backup codes to present to the user.
    /// </summary>
    /// <remarks>
    /// These codes are shown to the user once and are not stored in plaintext.
    /// Only SHA-256 hashes are persisted in the database.
    /// </remarks>
    public IReadOnlyList<string> Codes { get; set; } = [];
}
