using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Provides user authentication and account management operations.
/// </summary>
/// <remarks>
/// This service validates credentials, manages user lifecycle, and initiates
/// token-related workflows. Actual JWT token issuance is performed by OpenIddict at
/// the HTTP endpoint layer (Phase 0.7); this service handles the domain logic.
/// </remarks>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user account.
    /// </summary>
    /// <param name="request">Registration details including email, password, and profile info.</param>
    /// <param name="caller">Context identifying who initiated the registration.</param>
    /// <returns>A response containing the new user's ID and whether email confirmation is required.</returns>
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CallerContext caller);

    /// <summary>
    /// Validates user credentials and returns authenticated user information.
    /// </summary>
    /// <param name="request">Login credentials (email, password, optional TOTP code).</param>
    /// <param name="caller">Context identifying the caller.</param>
    /// <returns>
    /// User identity and placeholder token data. The access/refresh token fields are
    /// populated by the OAuth2 endpoint layer using OpenIddict.
    /// </returns>
    Task<LoginResponse> LoginAsync(LoginRequest request, CallerContext caller);

    /// <summary>
    /// Revokes all active tokens for the specified user (logout).
    /// </summary>
    /// <param name="userId">The ID of the user to log out.</param>
    /// <param name="refreshToken">Optional specific refresh token to revoke; if null all tokens are revoked.</param>
    /// <param name="caller">Context identifying the caller.</param>
    Task LogoutAsync(Guid userId, string? refreshToken, CallerContext caller);

    /// <summary>
    /// Exchanges a valid refresh token for a new access/refresh token pair.
    /// </summary>
    /// <param name="request">The refresh token to exchange.</param>
    /// <param name="caller">Context identifying the caller.</param>
    /// <returns>A new token pair, or an error if the refresh token is invalid or expired.</returns>
    Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CallerContext caller);

    /// <summary>
    /// Confirms a user's email address using the token sent to their inbox.
    /// </summary>
    /// <param name="userId">The ID of the user whose email is being confirmed.</param>
    /// <param name="token">The email confirmation token.</param>
    /// <returns><see langword="true"/> if the email was confirmed successfully; otherwise <see langword="false"/>.</returns>
    Task<bool> ConfirmEmailAsync(Guid userId, string token);

    /// <summary>
    /// Generates a password reset token and logs it (email delivery is Phase 0.x).
    /// </summary>
    /// <param name="email">The email address of the account requesting the reset.</param>
    Task InitiatePasswordResetAsync(string email);

    /// <summary>
    /// Resets the user's password using a valid reset token.
    /// </summary>
    /// <param name="request">Reset details including email, token, and new password.</param>
    /// <returns><see langword="true"/> if the password was reset successfully; otherwise <see langword="false"/>.</returns>
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>
    /// Changes the authenticated user's password after verifying the current password.
    /// </summary>
    /// <param name="userId">The ID of the user changing their password.</param>
    /// <param name="request">Request containing the current and new passwords.</param>
    /// <returns><see langword="true"/> if the password was changed successfully; otherwise <see langword="false"/>.</returns>
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);

    /// <summary>
    /// Gets the current authenticated user's profile information.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The user profile, or <see langword="null"/> if the user was not found.</returns>
    Task<UserProfileResponse?> GetUserProfileAsync(Guid userId);
}
