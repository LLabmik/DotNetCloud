using System;
using System.ComponentModel.DataAnnotations;

namespace DotNetCloud.Core.Data.Entities.Auth;

/// <summary>
/// Represents an OAuth2/OIDC client application registered in the OpenIddict system.
/// </summary>
/// <remarks>
/// This entity stores registered clients (applications) that can request tokens from the authorization server.
/// It includes client credentials, redirect URIs, scopes, and other OAuth2/OIDC-specific configuration.
/// 
/// <para>
/// <b>Entity Relationships:</b>
/// <list type="bullet">
/// <item>One-to-many with <see cref="OpenIddictAuthorization"/> (one client can have many authorizations)</item>
/// <item>One-to-many with <see cref="OpenIddictToken"/> (one client can have many tokens)</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Key Properties:</b>
/// <list type="bullet">
/// <item><see cref="ClientId"/>: Unique identifier for the client (e.g., "dotnetcloud-web-app")</item>
/// <item><see cref="ClientSecret"/>: Hashed secret for confidential clients</item>
/// <item><see cref="RedirectUris"/>: JSON array of allowed redirect URIs</item>
/// <item><see cref="Permissions"/>: JSON array of granted permissions (scopes, grant types, etc.)</item>
/// <item><see cref="Type"/>: Client type (confidential, public, hybrid)</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Security Considerations:</b>
/// <list type="bullet">
/// <item>Client secrets are stored hashed (never in plaintext)</item>
/// <item>Redirect URIs must be validated to prevent open redirect attacks</item>
/// <item>PKCE should be required for public clients</item>
/// <item>Grant types should be restricted based on client type</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// var client = new OpenIddictApplication
/// {
///     ClientId = "dotnetcloud-web-app",
///     DisplayName = "DotNetCloud Web Application",
///     Type = "confidential",
///     RedirectUris = "[\"https://localhost:5001/signin-oidc\"]",
///     Permissions = "[\"ept:token\", \"ept:authorization\", \"gt:authorization_code\", \"gt:refresh_token\", \"scp:openid\", \"scp:profile\", \"scp:email\"]",
///     Requirements = "[\"pkce\"]"
/// };
/// </code>
/// </para>
/// </remarks>
public class OpenIddictApplication
{
    /// <summary>
    /// Gets or sets the unique identifier for the application.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the client identifier (e.g., "dotnetcloud-web-app").
    /// </summary>
    /// <remarks>
    /// This is the public identifier used by clients in OAuth2/OIDC flows.
    /// Must be unique across all registered applications.
    /// Typically follows DNS-style naming conventions (e.g., "com.example.myapp").
    /// </remarks>
    [Required]
    [MaxLength(200)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hashed client secret for confidential clients.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only used for confidential clients (e.g., web applications running on a server).
    /// Public clients (e.g., mobile apps, SPAs) should not have a client secret.
    /// </para>
    /// <para>
    /// <b>Security:</b> The secret is hashed using a secure algorithm (e.g., BCrypt, PBKDF2).
    /// Never store secrets in plaintext.
    /// </para>
    /// </remarks>
    [MaxLength(500)]
    public string? ClientSecret { get; set; }

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
    /// Gets or sets the consent type (explicit, implicit, external, systematic).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>explicit</b>: User must grant consent for each authorization request</item>
    /// <item><b>implicit</b>: Consent is granted automatically (trusted first-party clients)</item>
    /// <item><b>external</b>: Consent is managed by an external identity provider</item>
    /// <item><b>systematic</b>: No consent required (system-to-system communication)</item>
    /// </list>
    /// </remarks>
    [MaxLength(50)]
    public string? ConsentType { get; set; }

    /// <summary>
    /// Gets or sets the display name of the application shown to users during consent.
    /// </summary>
    /// <remarks>
    /// This is the friendly name shown to users when granting permissions.
    /// Example: "DotNetCloud Web Application" instead of "dotnetcloud-web-app".
    /// </remarks>
    [MaxLength(200)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the localized display names as a JSON object.
    /// </summary>
    /// <remarks>
    /// Format: <c>{ "en": "DotNetCloud Web App", "fr": "Application Web DotNetCloud" }</c>
    /// </remarks>
    [MaxLength(2000)]
    public string? DisplayNames { get; set; }

    /// <summary>
    /// Gets or sets the JSON serialized string representation of the custom properties.
    /// </summary>
    /// <remarks>
    /// Stores additional application-specific metadata as JSON.
    /// Example: <c>{ "LogoUrl": "https://example.com/logo.png", "SupportEmail": "support@example.com" }</c>
    /// </remarks>
    public string? JsonWebKeySet { get; set; }

    /// <summary>
    /// Gets or sets the permissions granted to the application as a JSON array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Format: <c>["ept:token", "ept:authorization", "gt:authorization_code", "gt:refresh_token", "scp:openid", "scp:profile"]</c>
    /// </para>
    /// <para>
    /// <b>Permission Prefixes:</b>
    /// <list type="bullet">
    /// <item><b>ept:</b> Endpoint permissions (token, authorization, introspection, revocation, userinfo)</item>
    /// <item><b>gt:</b> Grant type permissions (authorization_code, implicit, password, client_credentials, refresh_token)</item>
    /// <item><b>scp:</b> Scope permissions (openid, profile, email, offline_access, custom scopes)</item>
    /// <item><b>rst:</b> Response type permissions (code, id_token, token)</item>
    /// <item><b>rchl:</b> Response mode permissions (query, fragment, form_post)</item>
    /// </list>
    /// </para>
    /// </remarks>
    public string? Permissions { get; set; }

    /// <summary>
    /// Gets or sets the post-logout redirect URIs as a JSON array.
    /// </summary>
    /// <remarks>
    /// URIs where the user can be redirected after logout.
    /// Format: <c>["https://localhost:5001/signout-callback-oidc"]</c>
    /// Must be exact matches for security.
    /// </remarks>
    public string? PostLogoutRedirectUris { get; set; }

    /// <summary>
    /// Gets or sets the custom properties as a JSON object.
    /// </summary>
    /// <remarks>
    /// Stores additional application-specific metadata.
    /// Example: <c>{ "AppVersion": "1.0.0", "OrganizationId": "123e4567-e89b-12d3-a456-426614174000" }</c>
    /// </remarks>
    public string? Properties { get; set; }

    /// <summary>
    /// Gets or sets the redirect URIs as a JSON array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// URIs where the authorization server can send responses (authorization codes, tokens).
    /// Format: <c>["https://localhost:5001/signin-oidc", "https://app.example.com/callback"]</c>
    /// </para>
    /// <para>
    /// <b>Security:</b> These URIs must be exact matches to prevent open redirect attacks.
    /// Wildcard URIs are not allowed for security reasons.
    /// </para>
    /// </remarks>
    public string? RedirectUris { get; set; }

    /// <summary>
    /// Gets or sets the requirements applied to the application as a JSON array.
    /// </summary>
    /// <remarks>
    /// Format: <c>["pkce"]</c> for requiring Proof Key for Code Exchange.
    /// <para>
    /// <b>Common Requirements:</b>
    /// <list type="bullet">
    /// <item><b>pkce</b>: Requires Proof Key for Code Exchange (recommended for public clients)</item>
    /// </list>
    /// </para>
    /// </remarks>
    public string? Requirements { get; set; }

    /// <summary>
    /// Gets or sets the client type (confidential, public, or hybrid).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>confidential</b>: Client can securely store secrets (server-side web apps)</item>
    /// <item><b>public</b>: Client cannot securely store secrets (SPAs, mobile apps, desktop apps)</item>
    /// <item><b>hybrid</b>: Supports both confidential and public flows (rare)</item>
    /// </list>
    /// </remarks>
    [MaxLength(50)]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the settings as a JSON object.
    /// </summary>
    /// <remarks>
    /// Stores additional configuration settings for the application.
    /// Example: <c>{ "TokenLifetime": 3600, "RefreshTokenLifetime": 86400 }</c>
    /// </remarks>
    public string? Settings { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the collection of authorizations associated with this application.
    /// </summary>
    public virtual ICollection<OpenIddictAuthorization> Authorizations { get; set; } = new List<OpenIddictAuthorization>();

    /// <summary>
    /// Gets or sets the collection of tokens associated with this application.
    /// </summary>
    public virtual ICollection<OpenIddictToken> Tokens { get; set; } = new List<OpenIddictToken>();
}
