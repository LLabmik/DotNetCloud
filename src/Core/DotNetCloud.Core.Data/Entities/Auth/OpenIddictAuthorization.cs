using System;
using System.ComponentModel.DataAnnotations;
using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Entities.Auth;

/// <summary>
/// Represents an authorization (consent) granted by a user to an application.
/// </summary>
/// <remarks>
/// This entity stores user consent records for OAuth2/OIDC authorization requests.
/// It tracks which users have authorized which applications to access their data with specific scopes.
/// 
/// <para>
/// <b>Entity Relationships:</b>
/// <list type="bullet">
/// <item>Many-to-one with <see cref="OpenIddictApplication"/> (many authorizations can belong to one application)</item>
/// <item>Many-to-one with <see cref="ApplicationUser"/> (many authorizations can belong to one user)</item>
/// <item>One-to-many with <see cref="OpenIddictToken"/> (one authorization can have many tokens)</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Key Properties:</b>
/// <list type="bullet">
/// <item><see cref="Status"/>: Authorization status (valid, revoked)</item>
/// <item><see cref="Scopes"/>: JSON array of granted scopes (e.g., ["openid", "profile", "email"])</item>
/// <item><see cref="Type"/>: Authorization type (permanent, ad-hoc)</item>
/// <item><see cref="CreationDate"/>: When the user granted consent</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Authorization Types:</b>
/// <list type="bullet">
/// <item><b>permanent</b>: Long-lived consent that persists across sessions</item>
/// <item><b>ad-hoc</b>: Short-lived, single-use authorization</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Status Values:</b>
/// <list type="bullet">
/// <item><b>valid</b>: Authorization is active and can be used</item>
/// <item><b>revoked</b>: Authorization has been revoked by the user or admin</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// var authorization = new OpenIddictAuthorization
/// {
///     ApplicationId = clientId,
///     Subject = userId.ToString(),
///     Status = "valid",
///     Type = "permanent",
///     Scopes = "[\"openid\", \"profile\", \"email\"]",
///     CreationDate = DateTime.UtcNow
/// };
/// </code>
/// </para>
/// </remarks>
public class OpenIddictAuthorization
{
    /// <summary>
    /// Gets or sets the unique identifier for the authorization.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the application identifier this authorization is associated with.
    /// </summary>
    /// <remarks>
    /// Foreign key to the <see cref="OpenIddictApplication"/> entity.
    /// Identifies which client application the user has authorized.
    /// </remarks>
    public Guid? ApplicationId { get; set; }

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
    /// Gets or sets the UTC date and time when the authorization was created.
    /// </summary>
    /// <remarks>
    /// Tracks when the user initially granted consent.
    /// Useful for auditing and consent expiration logic.
    /// </remarks>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Gets or sets the custom properties as a JSON object.
    /// </summary>
    /// <remarks>
    /// Stores additional authorization-specific metadata.
    /// Example: <c>{ "OrganizationId": "123e4567-e89b-12d3-a456-426614174000", "ExpiresAt": "2024-12-31T23:59:59Z" }</c>
    /// </remarks>
    public string? Properties { get; set; }

    /// <summary>
    /// Gets or sets the granted scopes as a JSON array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Format: <c>["openid", "profile", "email", "files.read", "files.write"]</c>
    /// </para>
    /// <para>
    /// Defines what data and operations the application is authorized to access.
    /// Scopes should follow the principle of least privilege.
    /// </para>
    /// </remarks>
    public string? Scopes { get; set; }

    /// <summary>
    /// Gets or sets the authorization status (valid, revoked).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>valid</b>: Authorization is active and can be used to issue tokens</item>
    /// <item><b>revoked</b>: Authorization has been revoked and cannot be used</item>
    /// </list>
    /// <para>
    /// When status is "revoked", associated tokens should be invalidated.
    /// </para>
    /// </remarks>
    [MaxLength(50)]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the subject (user identifier) who granted the authorization.
    /// </summary>
    /// <remarks>
    /// This is typically the <see cref="ApplicationUser.Id"/> as a string.
    /// Format: <c>"123e4567-e89b-12d3-a456-426614174000"</c>
    /// </remarks>
    [MaxLength(200)]
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the authorization type (permanent, ad-hoc).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>permanent</b>: Long-lived authorization that persists across sessions (typical for user consent)</item>
    /// <item><b>ad-hoc</b>: Short-lived, single-use authorization (used for temporary grants)</item>
    /// </list>
    /// </remarks>
    [MaxLength(50)]
    public string? Type { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the application this authorization is associated with.
    /// </summary>
    /// <remarks>
    /// Navigation property to the <see cref="OpenIddictApplication"/> entity.
    /// </remarks>
    public virtual OpenIddictApplication? Application { get; set; }

    /// <summary>
    /// Gets or sets the collection of tokens associated with this authorization.
    /// </summary>
    /// <remarks>
    /// Tokens issued using this authorization are tracked for revocation purposes.
    /// When an authorization is revoked, all associated tokens should be invalidated.
    /// </remarks>
    public virtual ICollection<OpenIddictToken> Tokens { get; set; } = new List<OpenIddictToken>();
}
