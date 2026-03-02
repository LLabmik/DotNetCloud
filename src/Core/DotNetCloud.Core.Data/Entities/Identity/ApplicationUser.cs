using Microsoft.AspNetCore.Identity;

namespace DotNetCloud.Core.Data.Entities.Identity;

/// <summary>
/// Represents a user in the DotNetCloud system.
/// Extends ASP.NET Core Identity's IdentityUser with application-specific properties.
/// Uses Guid as the primary key type.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Gets or sets the display name for the user.
    /// This is the user-friendly name shown in the UI.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the URL to the user's avatar image.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the user's preferred locale/language code (e.g., "en-US", "fr-FR").
    /// Default is "en-US".
    /// </summary>
    public string Locale { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets the user's timezone identifier (e.g., "America/New_York", "Europe/Paris").
    /// Default is "UTC".
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Gets or sets when the user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last time the user logged in.
    /// Null if the user has never logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Gets or sets whether the user account is active.
    /// Inactive users cannot log in.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
