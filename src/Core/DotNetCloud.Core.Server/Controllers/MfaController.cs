using DotNetCloud.Core.Auth;
using DotNetCloud.Core.Dtos.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Multi-factor authentication (MFA) endpoints for TOTP setup, verification, and backup codes.
/// </summary>
[ApiController]
[Route("api/v1/auth/mfa")]
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

    /// <summary>
    /// Get TOTP setup information including QR code for authenticator app.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TOTP setup response with QR code and shared key</returns>
    [HttpGet("totp-setup")]
    public async Task<IActionResult> GetTotpSetupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
            }

            var response = await _mfaService.GetTotpSetupAsync(userId, cancellationToken);
            _logger.LogInformation("TOTP setup initiated for user {UserId}", userId);
            return Ok(new { success = true, data = response });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("TOTP setup failed: {Message}", ex.Message);
            return BadRequest(new { success = false, error = new { code = "TOTP_SETUP_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Verify TOTP code and enable two-factor authentication.
    /// </summary>
    /// <param name="request">TOTP verification request with code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Verification response with backup codes</returns>
    [HttpPost("totp-verify")]
    public async Task<IActionResult> VerifyTotpAsync([FromBody] TotpVerifyRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
            }

            var response = await _mfaService.VerifyTotpAsync(userId, request.Code, cancellationToken);
            _logger.LogInformation("TOTP verified and enabled for user {UserId}", userId);
            return Ok(new { success = true, data = response });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "INVALID_CODE", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("TOTP verification failed: {Message}", ex.Message);
            return BadRequest(new { success = false, error = new { code = "VERIFICATION_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Disable TOTP authentication.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confirmation that TOTP was disabled</returns>
    [HttpPost("totp-disable")]
    public async Task<IActionResult> DisableTotpAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
            }

            await _mfaService.DisableTotpAsync(userId, cancellationToken);
            _logger.LogInformation("TOTP disabled for user {UserId}", userId);
            return Ok(new { success = true, message = "TOTP has been disabled" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "DISABLE_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Generate new backup codes for account recovery.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New backup codes for account recovery</returns>
    [HttpPost("backup-codes")]
    public async Task<IActionResult> GenerateBackupCodesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
            }

            var response = await _mfaService.GenerateBackupCodesAsync(userId, cancellationToken);
            _logger.LogInformation("Backup codes generated for user {UserId}", userId);
            return Ok(new { success = true, data = response });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "GENERATION_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Get MFA status for the current user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current MFA status and enabled methods</returns>
    [HttpGet("status")]
    public async Task<IActionResult> GetMfaStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
            }

            var status = await _mfaService.GetMfaStatusAsync(userId, cancellationToken);
            return Ok(new { success = true, data = status });
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found" } });
        }
    }
}
