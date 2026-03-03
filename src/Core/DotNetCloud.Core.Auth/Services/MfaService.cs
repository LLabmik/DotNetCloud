using System.Security.Cryptography;
using System.Text;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Auth;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace DotNetCloud.Core.Auth.Services;

/// <summary>
/// Implements <see cref="IMfaService"/> using ASP.NET Core Identity TOTP and the
/// <see cref="UserBackupCode"/> table for hashed backup codes.
/// </summary>
public sealed class MfaService : IMfaService
{
    private const int BackupCodeCount = 10;
    private const int BackupCodeLength = 8;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CoreDbContext _dbContext;
    private readonly ILogger<MfaService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MfaService"/>.
    /// </summary>
    public MfaService(
        UserManager<ApplicationUser> userManager,
        CoreDbContext dbContext,
        ILogger<MfaService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TotpSetupResponse> GetTotpSetupAsync(Guid userId)
    {
        var user = await FindUserAsync(userId);

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = user.Email ?? user.UserName ?? userId.ToString();
        var qrCodeUri = GenerateQrCodeUri(email, key!);

        var remainingCodes = await _dbContext.UserBackupCodes
            .CountAsync(c => c.UserId == userId && !c.IsUsed);

        return new TotpSetupResponse
        {
            SharedKey = FormatKey(key!),
            QrCodeUri = qrCodeUri,
            RecoveryCodesRemaining = remainingCodes,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyTotpAsync(Guid userId, string code)
    {
        var user = await FindUserAsync(userId);

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            code);

        if (isValid)
        {
            _logger.LogInformation("TOTP verified successfully for user {UserId}", userId);
        }
        else
        {
            _logger.LogWarning("TOTP verification failed for user {UserId}", userId);
        }

        return isValid;
    }

    /// <inheritdoc/>
    public async Task<BackupCodesResponse> GenerateBackupCodesAsync(Guid userId)
    {
        // Invalidate existing codes by removing them
        var existingCodes = await _dbContext.UserBackupCodes
            .Where(c => c.UserId == userId)
            .ToListAsync();

        _dbContext.UserBackupCodes.RemoveRange(existingCodes);

        var plaintextCodes = new List<string>(BackupCodeCount);
        var now = DateTime.UtcNow;

        for (var i = 0; i < BackupCodeCount; i++)
        {
            var code = GenerateRandomCode(BackupCodeLength);
            plaintextCodes.Add(code);

            _dbContext.UserBackupCodes.Add(new UserBackupCode
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CodeHash = HashCode(code),
                IsUsed = false,
                CreatedAt = now,
            });
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation(
            "Generated {Count} backup codes for user {UserId}", BackupCodeCount, userId);

        return new BackupCodesResponse { Codes = plaintextCodes.AsReadOnly() };
    }

    /// <inheritdoc/>
    public async Task<bool> UseBackupCodeAsync(Guid userId, string code)
    {
        var hash = HashCode(code);

        var backupCode = await _dbContext.UserBackupCodes
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CodeHash == hash && !c.IsUsed);

        if (backupCode is null)
        {
            _logger.LogWarning("Backup code redemption failed for user {UserId}", userId);
            return false;
        }

        backupCode.IsUsed = true;
        backupCode.UsedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Backup code redeemed for user {UserId}", userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task DisableMfaAsync(Guid userId)
    {
        var user = await FindUserAsync(userId);

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);

        // Remove all backup codes
        var codes = await _dbContext.UserBackupCodes
            .Where(c => c.UserId == userId)
            .ToListAsync();

        _dbContext.UserBackupCodes.RemoveRange(codes);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("MFA disabled for user {UserId}", userId);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task<ApplicationUser> FindUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new InvalidOperationException($"User {userId} not found.");
        }

        return user;
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.ToUpperInvariant()));
        return Convert.ToHexString(bytes);
    }

    private static string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var result = new char[length];
        using var rng = RandomNumberGenerator.Create();
        var buffer = new byte[length];
        rng.GetBytes(buffer);
        for (var i = 0; i < length; i++)
        {
            result[i] = chars[buffer[i] % chars.Length];
        }

        return new string(result);
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4));
            result.Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private static string GenerateQrCodeUri(string email, string unformattedKey)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedKey = Uri.EscapeDataString(unformattedKey);
        return $"otpauth://totp/DotNetCloud:{encodedEmail}?secret={encodedKey}&issuer=DotNetCloud";
    }
}
