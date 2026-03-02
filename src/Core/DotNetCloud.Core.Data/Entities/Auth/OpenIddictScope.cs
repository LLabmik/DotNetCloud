using System;
using System.ComponentModel.DataAnnotations;

namespace DotNetCloud.Core.Data.Entities.Auth;

/// <summary>
/// Represents an OAuth2/OIDC scope definition.
/// </summary>
/// <remarks>
/// This entity defines the available scopes (permissions) that applications can request.
/// Scopes represent specific permissions or sets of permissions (e.g., "profile", "email", "files.read").
/// 
/// <para>
/// <b>Purpose:</b>
/// <list type="bullet">
/// <item>Define available scopes in the system</item>
/// <item>Provide user-friendly descriptions for consent screens</item>
/// <item>Associate resources with scopes</item>
/// <item>Support localized scope names and descriptions</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Key Properties:</b>
/// <list type="bullet">
/// <item><see cref="Name"/>: Unique scope identifier (e.g., "openid", "profile", "files.read")</item>
/// <item><see cref="DisplayName"/>: User-friendly name shown during consent</item>
/// <item><see cref="Description"/>: Detailed description of what the scope grants</item>
/// <item><see cref="Resources"/>: Associated resource servers (APIs)</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Standard OIDC Scopes:</b>
/// <list type="bullet">
/// <item><b>openid</b>: Indicates an OIDC authentication request (returns id_token)</item>
/// <item><b>profile</b>: Access to user's profile information (name, picture, etc.)</item>
/// <item><b>email</b>: Access to user's email address</item>
/// <item><b>address</b>: Access to user's postal address</item>
/// <item><b>phone</b>: Access to user's phone number</item>
/// <item><b>offline_access</b>: Request a refresh token</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Custom Scope Examples:</b>
/// <list type="bullet">
/// <item><b>files.read</b>: Read access to files</item>
/// <item><b>files.write</b>: Write access to files</item>
/// <item><b>chat.send</b>: Send messages in chat</item>
/// <item><b>calendar.events</b>: Access to calendar events</item>
/// <item><b>admin</b>: Administrative access</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// var scope = new OpenIddictScope
/// {
///     Name = "files.read",
///     DisplayName = "Read Files",
///     Description = "Allows the application to read your files",
///     Resources = "[\"files_api\"]"
/// };
/// </code>
/// </para>
/// </remarks>
public class OpenIddictScope
{
    /// <summary>
    /// Gets or sets the unique identifier for the scope.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

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
    /// Gets or sets the scope description.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A detailed description of what the scope grants access to.
    /// Shown to users during the consent process.
    /// </para>
    /// <para>
    /// Example: "Allows the application to read and list your files"
    /// </para>
    /// </remarks>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the localized scope descriptions as a JSON object.
    /// </summary>
    /// <remarks>
    /// Format: <c>{ "en": "Read files", "fr": "Lire les fichiers", "es": "Leer archivos" }</c>
    /// Enables consent screens in multiple languages.
    /// </remarks>
    [MaxLength(2000)]
    public string? Descriptions { get; set; }

    /// <summary>
    /// Gets or sets the scope display name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A user-friendly name shown during consent.
    /// Example: "Read Files" instead of "files.read"
    /// </para>
    /// </remarks>
    [MaxLength(200)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the localized scope display names as a JSON object.
    /// </summary>
    /// <remarks>
    /// Format: <c>{ "en": "Read Files", "fr": "Lire les fichiers", "es": "Leer archivos" }</c>
    /// Enables consent screens in multiple languages.
    /// </remarks>
    [MaxLength(2000)]
    public string? DisplayNames { get; set; }

    /// <summary>
    /// Gets or sets the scope name (unique identifier).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The unique name of the scope used in OAuth2/OIDC requests.
    /// Must be unique across all scopes.
    /// </para>
    /// <para>
    /// <b>Naming Conventions:</b>
    /// <list type="bullet">
    /// <item>Standard OIDC scopes: lowercase (e.g., "openid", "profile", "email")</item>
    /// <item>Custom scopes: dot-notation (e.g., "files.read", "chat.send")</item>
    /// <item>Administrative scopes: descriptive names (e.g., "admin", "moderator")</item>
    /// </list>
    /// </para>
    /// </remarks>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the custom properties as a JSON object.
    /// </summary>
    /// <remarks>
    /// Stores additional scope-specific metadata.
    /// Example: <c>{ "Category": "File Management", "RequiresApproval": true }</c>
    /// </remarks>
    public string? Properties { get; set; }

    /// <summary>
    /// Gets or sets the associated resource servers (APIs) as a JSON array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Format: <c>["files_api", "storage_api"]</c>
    /// </para>
    /// <para>
    /// Defines which resource servers (APIs) this scope applies to.
    /// Used for audience restriction in access tokens.
    /// </para>
    /// <para>
    /// <b>Example:</b>
    /// If a scope "files.read" is associated with resource "files_api",
    /// tokens with this scope can only access the Files API.
    /// </para>
    /// </remarks>
    public string? Resources { get; set; }
}
