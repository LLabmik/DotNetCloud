using System;
using System.ComponentModel.DataAnnotations;

namespace DotNetCloud.Core.Data.Entities.Auth;

/// <summary>
/// Represents an OAuth2/OIDC token (access token, refresh token, ID token, etc.).
/// </summary>
/// <remarks>
/// This entity stores issued tokens for tracking, validation, and revocation purposes.
/// It supports multiple token types and formats (JWT, reference tokens, etc.).
/// 
/// <para>
/// <b>Entity Relationships:</b>
/// <list type="bullet">
/// <item>Many-to-one with <see cref="OpenIddictApplication"/> (many tokens can belong to one application)</item>
/// <item>Many-to-one with <see cref="OpenIddictAuthorization"/> (many tokens can belong to one authorization)</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Key Properties:</b>
/// <list type="bullet">
/// <item><see cref="Type"/>: Token type (access_token, refresh_token, id_token, authorization_code)</item>
/// <item><see cref="Status"/>: Token status (valid, revoked, redeemed)</item>
/// <item><see cref="ReferenceId"/>: Unique reference ID for token lookups</item>
/// <item><see cref="Payload"/>: Token payload (JWT, JSON, or reference)</item>
/// <item><see cref="ExpirationDate"/>: When the token expires</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Token Types:</b>
/// <list type="bullet">
/// <item><b>access_token</b>: Short-lived token for accessing protected resources</item>
/// <item><b>refresh_token</b>: Long-lived token for obtaining new access tokens</item>
/// <item><b>id_token</b>: JWT containing user identity claims (OIDC)</item>
/// <item><b>authorization_code</b>: Short-lived code for authorization code flow</item>
/// <item><b>device_code</b>: Code for device authorization flow</item>
/// <item><b>user_code</b>: User-friendly code for device authorization flow</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Status Values:</b>
/// <list type="bullet">
/// <item><b>valid</b>: Token is active and can be used</item>
/// <item><b>revoked</b>: Token has been revoked and cannot be used</item>
/// <item><b>redeemed</b>: Token has been used (applies to authorization codes)</item>
/// <item><b>inactive</b>: Token is no longer active (expired or revoked)</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Security Considerations:</b>
/// <list type="bullet">
/// <item>Tokens should be stored encrypted or hashed when possible</item>
/// <item>Expired tokens should be periodically purged</item>
/// <item>Refresh tokens should be rotated after use for enhanced security</item>
/// <item>Authorization codes must be single-use (status = redeemed)</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// var token = new OpenIddictToken
/// {
///     ApplicationId = clientId,
///     AuthorizationId = authorizationId,
///     Subject = userId.ToString(),
///     Type = "access_token",
///     Status = "valid",
///     ReferenceId = Guid.NewGuid().ToString(),
///     Payload = jwtPayload,
///     CreationDate = DateTime.UtcNow,
///     ExpirationDate = DateTime.UtcNow.AddHours(1)
/// };
/// </code>
/// </para>
/// </remarks>
public class OpenIddictToken
{
    /// <summary>
    /// Gets or sets the unique identifier for the token.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the application identifier this token is associated with.
    /// </summary>
    /// <remarks>
    /// Foreign key to the <see cref="OpenIddictApplication"/> entity.
    /// Identifies which client application the token was issued to.
    /// </remarks>
    public Guid? ApplicationId { get; set; }

    /// <summary>
    /// Gets or sets the authorization identifier this token is associated with.
    /// </summary>
    /// <remarks>
    /// Foreign key to the <see cref="OpenIddictAuthorization"/> entity.
    /// Links the token to the user's consent/authorization record.
    /// Can be null for client credentials grant (no user involvement).
    /// </remarks>
    public Guid? AuthorizationId { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token used for optimistic concurrency control.
    /// </summary>
    /// <remarks>
    /// Used to detect concurrent modifications to the same record.
    /// Updated automatically by EF Core when the entity is modified.
    /// </remarks>
    [ConcurrencyCheck]
    [MaxLength(50)]
    public string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the UTC date and time when the token was created.
    /// </summary>
    /// <remarks>
    /// Tracks when the token was issued.
    /// Useful for auditing and security monitoring.
    /// </remarks>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the token expires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After this date, the token is no longer valid and cannot be used.
    /// The authorization server should reject expired tokens.
    /// </para>
    /// <para>
    /// <b>Typical Lifetimes:</b>
    /// <list type="bullet">
    /// <item>Access tokens: 1 hour</item>
    /// <item>Refresh tokens: 7-30 days</item>
    /// <item>Authorization codes: 5 minutes</item>
    /// <item>ID tokens: 1 hour (rarely validated, used once after issuance)</item>
    /// </list>
    /// </para>
    /// </remarks>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Gets or sets the token payload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For JWT tokens: Contains the full JWT string.
    /// For reference tokens: Contains a random identifier (ReferenceId).
    /// For authorization codes: Contains the authorization code value.
    /// </para>
    /// <para>
    /// <b>Security:</b> Sensitive tokens (especially refresh tokens) should be hashed or encrypted.
    /// </para>
    /// </remarks>
    public string? Payload { get; set; }

    /// <summary>
    /// Gets or sets the custom properties as a JSON object.
    /// </summary>
    /// <remarks>
    /// Stores additional token-specific metadata.
    /// Example: <c>{ "DeviceId": "device-123", "IpAddress": "192.168.1.1" }</c>
    /// </remarks>
    public string? Properties { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the authorization code was redeemed.
    /// </summary>
    /// <remarks>
    /// Only applicable to authorization codes.
    /// Once redeemed, the code cannot be used again (prevents replay attacks).
    /// </remarks>
    public DateTime? RedemptionDate { get; set; }

    /// <summary>
    /// Gets or sets the reference identifier for the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A unique, random identifier used for token lookups and validation.
    /// Typically a GUID or base64-encoded random string.
    /// </para>
    /// <para>
    /// <b>Usage:</b>
    /// <list type="bullet">
    /// <item>Reference tokens: Used as the token value itself</item>
    /// <item>JWT tokens: Used for revocation checks (JTI claim)</item>
    /// <item>Authorization codes: Used as the code value</item>
    /// </list>
    /// </para>
    /// </remarks>
    [MaxLength(200)]
    public string? ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the token status (valid, revoked, redeemed, inactive).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>valid</b>: Token is active and can be used</item>
    /// <item><b>revoked</b>: Token has been explicitly revoked</item>
    /// <item><b>redeemed</b>: Token has been used (applies to authorization codes)</item>
    /// <item><b>inactive</b>: Token is no longer active (expired or otherwise unusable)</item>
    /// </list>
    /// <para>
    /// Status is checked during token validation to prevent unauthorized access.
    /// </para>
    /// </remarks>
    [MaxLength(50)]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the subject (user identifier) the token is issued to.
    /// </summary>
    /// <remarks>
    /// This is typically the <see cref="ApplicationUser.Id"/> as a string.
    /// Format: <c>"123e4567-e89b-12d3-a456-426614174000"</c>
    /// Can be null for client credentials grant (machine-to-machine).
    /// </remarks>
    [MaxLength(200)]
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the token type (access_token, refresh_token, id_token, authorization_code, etc.).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>access_token</b>: Used to access protected resources</item>
    /// <item><b>refresh_token</b>: Used to obtain new access tokens</item>
    /// <item><b>id_token</b>: Contains user identity claims (OIDC)</item>
    /// <item><b>authorization_code</b>: Temporary code for authorization code flow</item>
    /// <item><b>device_code</b>: Code for device authorization flow</item>
    /// <item><b>user_code</b>: User-friendly code for device authorization flow</item>
    /// </list>
    /// </remarks>
    [MaxLength(50)]
    public string? Type { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the application this token is associated with.
    /// </summary>
    /// <remarks>
    /// Navigation property to the <see cref="OpenIddictApplication"/> entity.
    /// </remarks>
    public virtual OpenIddictApplication? Application { get; set; }

    /// <summary>
    /// Gets or sets the authorization this token is associated with.
    /// </summary>
    /// <remarks>
    /// Navigation property to the <see cref="OpenIddictAuthorization"/> entity.
    /// Can be null for tokens issued without user consent (e.g., client credentials).
    /// </remarks>
    public virtual OpenIddictAuthorization? Authorization { get; set; }
}
