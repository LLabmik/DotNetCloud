using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace DotNetCloud.Core.Auth.Services;

/// <summary>
/// Invalidates every credential a user currently holds.
/// </summary>
/// <remarks>
/// Account-state transitions (disable, delete, password reset) are containment actions: if the
/// principal keeps a live access token, refresh token or cookie session, the control is defeated.
/// Every such transition must call <see cref="InvalidateAsync"/> so that nothing issued before the
/// change survives it.
/// </remarks>
public interface IAccountCredentialInvalidator
{
    /// <summary>
    /// Revokes all OpenIddict tokens issued to <paramref name="userId"/> and invalidates the user's
    /// security stamp, which rejects existing cookie tickets at the next revalidation.
    /// </summary>
    /// <param name="userId">The user whose credentials must stop working.</param>
    /// <param name="reason">A short reason for logging and audit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of token entries revoked.</returns>
    Task<int> InvalidateAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AccountCredentialInvalidator : IAccountCredentialInvalidator
{
    private readonly IOpenIddictTokenManager _tokenManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountCredentialInvalidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountCredentialInvalidator"/> class.
    /// </summary>
    /// <param name="tokenManager">OpenIddict token store.</param>
    /// <param name="userManager">Identity user manager, used to roll the security stamp.</param>
    /// <param name="logger">Logger.</param>
    public AccountCredentialInvalidator(
        IOpenIddictTokenManager tokenManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountCredentialInvalidator> logger)
    {
        _tokenManager = tokenManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> InvalidateAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var subject = userId.ToString();
        var revoked = 0;

        await foreach (var token in _tokenManager.FindBySubjectAsync(subject))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (await _tokenManager.TryRevokeAsync(token, cancellationToken))
                {
                    revoked++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Keep going: a single malformed entry must not leave the rest live.
                _logger.LogWarning(ex, "Could not revoke a token entry for user {UserId}", userId);
            }
        }

        // Roll the security stamp so cookie tickets fail revalidation. The user may already be
        // deleted, in which case there is nothing left to update.
        var user = await _userManager.FindByIdAsync(subject);
        if (user is not null)
        {
            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                _logger.LogWarning(
                    "Security stamp update failed for user {UserId}: {Errors}",
                    userId,
                    string.Join(", ", stampResult.Errors.Select(e => e.Description)));
            }
        }

        _logger.LogInformation(
            "Invalidated credentials for user {UserId} ({Reason}): {Revoked} token entr(y/ies) revoked",
            userId, reason, revoked);

        return revoked;
    }
}
