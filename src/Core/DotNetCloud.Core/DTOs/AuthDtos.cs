namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Request to authenticate a user with email and password.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TOTP code from the user's authenticator app, if MFA is enabled.
    /// </summary>
    public string? TotpCode { get; set; }
}

/// <summary>
/// Result of a successful login operation, containing user info and placeholder token data.
/// </summary>
/// <remarks>
/// The <see cref="AccessToken"/> and <see cref="RefreshToken"/> fields are populated by the
/// OAuth2/OIDC token endpoint in Phase 0.7. The domain service validates credentials and
/// returns user identity information; the web layer converts this into actual JWT tokens
/// via OpenIddict.
/// </remarks>
public sealed class LoginResponse
{
    /// <summary>
    /// Gets or sets the issued OAuth2 access token (populated at endpoint layer).
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the issued OAuth2 refresh token (populated at endpoint layer).
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the access token lifetime in seconds.
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the token type (always "Bearer").
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets the authenticated user's identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the authenticated user's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Request to create a new user account.
/// </summary>
public sealed class RegisterRequest
{
    /// <summary>
    /// Gets or sets the user's email address (used as login identifier).
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plaintext password (hashed before storage).
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's preferred display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's preferred locale (e.g., "en-US"). Defaults to "en-US".
    /// </summary>
    public string Locale { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets the user's preferred timezone (e.g., "UTC"). Defaults to "UTC".
    /// </summary>
    public string Timezone { get; set; } = "UTC";
}

/// <summary>
/// Result of a successful registration operation.
/// </summary>
public sealed class RegisterResponse
{
    /// <summary>
    /// Gets or sets the newly created user's identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the registered email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the user must confirm their email before logging in.
    /// </summary>
    public bool RequiresEmailConfirmation { get; set; }
}

/// <summary>
/// Request to exchange a refresh token for a new token pair.
/// </summary>
public sealed class RefreshTokenRequest
{
    /// <summary>
    /// Gets or sets the refresh token to exchange.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// A new token pair issued after a successful token refresh.
/// </summary>
public sealed class TokenResponse
{
    /// <summary>
    /// Gets or sets the new access token.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the access token lifetime in seconds.
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the token type (always "Bearer").
    /// </summary>
    public string TokenType { get; set; } = "Bearer";
}

/// <summary>
/// Request to revoke a token.
/// </summary>
public sealed class RevokeTokenRequest
{
    /// <summary>
    /// Gets or sets the token to revoke (access or refresh token).
    /// </summary>
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Request to change the currently authenticated user's password.
/// </summary>
public sealed class ChangePasswordRequest
{
    /// <summary>
    /// Gets or sets the user's current password for verification.
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the desired new password.
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Request to complete a password reset using a token from the reset email.
/// </summary>
public sealed class ResetPasswordRequest
{
    /// <summary>
    /// Gets or sets the email address of the account to reset.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password reset token received via email.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new password to set.
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Describes an error that occurred during an authentication operation.
/// </summary>
/// <param name="Code">A machine-readable error code (e.g., "InvalidCredentials").</param>
/// <param name="Description">A human-readable error description.</param>
public sealed record AuthError(string Code, string Description);
