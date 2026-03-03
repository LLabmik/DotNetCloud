using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Manages multi-factor authentication (TOTP and backup codes) for user accounts.
/// </summary>
public interface IMfaService
{
    /// <summary>
    /// Retrieves TOTP authenticator setup information for a user.
    /// </summary>
    /// <param name="userId">The ID of the user setting up TOTP.</param>
    /// <returns>The shared secret key, QR code URI, and remaining backup code count.</returns>
    Task<TotpSetupResponse> GetTotpSetupAsync(Guid userId);

    /// <summary>
    /// Verifies a TOTP code supplied by the user to complete or confirm setup.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="code">The 6-digit TOTP code to verify.</param>
    /// <returns><see langword="true"/> if the code is valid; otherwise <see langword="false"/>.</returns>
    Task<bool> VerifyTotpAsync(Guid userId, string code);

    /// <summary>
    /// Generates a new set of 10 single-use backup codes for the user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>
    /// The generated plaintext backup codes. These are shown to the user once;
    /// only SHA-256 hashes are stored in the database.
    /// </returns>
    /// <remarks>
    /// Calling this method invalidates any previously generated backup codes.
    /// </remarks>
    Task<BackupCodesResponse> GenerateBackupCodesAsync(Guid userId);

    /// <summary>
    /// Attempts to redeem a backup code as a second factor.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="code">The plaintext backup code supplied by the user.</param>
    /// <returns>
    /// <see langword="true"/> if a matching, unused code was found and marked as used;
    /// otherwise <see langword="false"/>.
    /// </returns>
    Task<bool> UseBackupCodeAsync(Guid userId, string code);

    /// <summary>
    /// Disables two-factor authentication for the specified user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    Task DisableMfaAsync(Guid userId);
}
