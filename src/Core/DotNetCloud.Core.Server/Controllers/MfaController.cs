using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Multi-factor authentication (MFA) endpoints for TOTP setup, verification, passkeys, and backup codes.
/// </summary>
[ApiController]
[Route("api/v1/core/auth/mfa")]
[Authorize]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly ILogger<MfaController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MfaController"/> class.
    /// </summary>
    public MfaController(IMfaService mfaService, ILogger<MfaController> logger)
    {
        _mfaService = mfaService ?? throw new ArgumentNullException(nameof(mfaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ---------------------------------------------------------------------------
    // TOTP
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Get TOTP authenticator setup information (shared key, QR code URI).
    /// </summary>
    /// <returns>TOTP setup response with shared key and provisioning URI.</returns>
    [HttpPost("totp/setup")]
    public async Task<IActionResult> SetupTotpAsync()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var response = await _mfaService.GetTotpSetupAsync(userId);
        _logger.LogInformation("TOTP setup retrieved for user {UserId}", userId);
        return Ok(new { success = true, data = response });
    }

    /// <summary>
    /// Verify a TOTP code to complete setup or confirm identity.
    /// </summary>
    /// <param name="request">Request containing the 6-digit TOTP code.</param>
    /// <returns>Whether the code was verified successfully.</returns>
    [HttpPost("totp/verify")]
    public async Task<IActionResult> VerifyTotpAsync([FromBody] TotpVerifyRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var verified = await _mfaService.VerifyTotpAsync(userId, request.Code);

        if (!verified)
        {
            return BadRequest(new { success = false, error = new { code = "INVALID_CODE", message = "The TOTP code is invalid or expired." } });
        }

        _logger.LogInformation("TOTP verified for user {UserId}", userId);
        return Ok(new { success = true, message = "TOTP verified successfully." });
    }

    /// <summary>
    /// Disable TOTP authentication for the current user.
    /// </summary>
    /// <returns>Confirmation that TOTP was disabled.</returns>
    [HttpPost("totp/disable")]
    public async Task<IActionResult> DisableTotpAsync()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        await _mfaService.DisableMfaAsync(userId);
        _logger.LogInformation("TOTP disabled for user {UserId}", userId);
        return Ok(new { success = true, message = "TOTP has been disabled." });
    }

    // ---------------------------------------------------------------------------
    // Passkey (FIDO2/WebAuthn) — skeleton endpoints
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Initiate passkey (FIDO2/WebAuthn) registration for the current user.
    /// </summary>
    /// <returns>Registration challenge options for the client.</returns>
    /// <remarks>
    /// Full FIDO2 implementation requires a WebAuthn library (e.g., FIDO2.NET).
    /// This endpoint provides the skeleton for future implementation.
    /// </remarks>
    [HttpPost("passkey/setup")]
    public IActionResult SetupPasskey()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        // FIDO2 registration challenge generation will be implemented when a WebAuthn library is integrated.
        // The FidoCredential entity is already in the database schema.
        _logger.LogInformation("Passkey setup requested for user {UserId}", userId);
        return Ok(new
        {
            success = true,
            data = new
            {
                message = "Passkey registration not yet fully implemented. FidoCredential entity is ready.",
                userId,
            }
        });
    }

    /// <summary>
    /// Verify a passkey assertion to authenticate the user.
    /// </summary>
    /// <returns>Whether the passkey assertion was verified successfully.</returns>
    /// <remarks>
    /// Full FIDO2 implementation requires a WebAuthn library (e.g., FIDO2.NET).
    /// This endpoint provides the skeleton for future implementation.
    /// </remarks>
    [HttpPost("passkey/verify")]
    public IActionResult VerifyPasskey()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        // FIDO2 assertion verification will be implemented when a WebAuthn library is integrated.
        _logger.LogInformation("Passkey verification requested for user {UserId}", userId);
        return Ok(new
        {
            success = true,
            data = new
            {
                message = "Passkey verification not yet fully implemented. FidoCredential entity is ready.",
                userId,
            }
        });
    }

    // ---------------------------------------------------------------------------
    // Backup Codes
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generate new backup codes for account recovery.
    /// </summary>
    /// <returns>New backup codes (shown once, stored as SHA-256 hashes).</returns>
    [HttpGet("backup-codes")]
    public async Task<IActionResult> GenerateBackupCodesAsync()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var response = await _mfaService.GenerateBackupCodesAsync(userId);
        _logger.LogInformation("Backup codes generated for user {UserId}", userId);
        return Ok(new { success = true, data = response });
    }

    // ---------------------------------------------------------------------------
    // MFA Status
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Get MFA status for the current user.
    /// </summary>
    /// <returns>Whether MFA is enabled and which methods are active.</returns>
    [HttpGet("status")]
    public IActionResult GetMfaStatus()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var hasTwoFactor = User.HasClaim("amr", "mfa");

        var status = new
        {
            userId,
            isMfaEnabled = hasTwoFactor,
            methods = hasTwoFactor ? new[] { "totp" } : Array.Empty<string>()
        };

        return Ok(new { success = true, data = status });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out userId);
    }
}
