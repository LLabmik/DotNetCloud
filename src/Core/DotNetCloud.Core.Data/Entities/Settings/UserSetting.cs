namespace DotNetCloud.Core.Data.Entities.Settings;

/// <summary>
/// Represents a user-scoped setting that applies to a specific user's preferences and configuration.
/// </summary>
/// <remarks>
/// User settings are personal configuration values that control behavior specific to an individual user.
/// Each user can customize settings to their preferences, such as UI themes, notification preferences,
/// and personal configuration.
/// 
/// Examples of user settings include:
/// - "ui.Theme" - User's preferred theme (light/dark/auto)
/// - "ui.Language" - User's preferred language
/// - "notifications.Email" - Whether user wants email notifications
/// - "notifications.Digest.Frequency" - How often digest emails are sent
/// - "storage.UploadPreferences" - User's default upload settings
/// - "security.TwoFactorEnabled" - Whether 2FA is enabled for this user
/// 
/// Some user settings may contain sensitive data (e.g., API keys, preferences with PII),
/// and should be encrypted in the database when marked with [Encrypted] attribute.
/// 
/// User settings take precedence over organization and system settings when present.
/// If a user setting is not defined, organization or system settings are used as fallback.
/// </remarks>
public class UserSetting
{
    /// <summary>
    /// Gets or sets the unique identifier for the setting.
    /// </summary>
    /// <remarks>
    /// Guid primary key for relational integrity across database providers.
    /// Auto-generated on creation.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user ID this setting belongs to.
    /// </summary>
    /// <remarks>
    /// Required. Foreign key reference to the ApplicationUser entity.
    /// Establishes the user scope for this setting.
    /// Cannot be null; each setting is tied to exactly one user.
    /// When a user is deleted, their settings should be cascade-deleted.
    /// </remarks>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the setting key that uniquely identifies this setting for the user.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 200 characters. Uses hierarchical naming convention with dots.
    /// Examples: "ui.Theme", "ui.Language", "notifications.Email", "notifications.Digest.Frequency".
    /// Combined with Module and UserId, must be unique.
    /// Keys are case-sensitive.
    /// </remarks>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the setting value as a JSON-serializable string.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 10,000 characters. Stores the actual setting value.
    /// Values are JSON-serializable to support complex types:
    /// - Simple types: "dark", "en-US", "true", "daily"
    /// - Complex types: "{\"provider\": \"smtp\", \"priority\": \"high\"}"
    /// 
    /// If marked with [Encrypted] attribute, this value should be encrypted at rest in the database.
    /// Consumers should decrypt and deserialize based on expected type.
    /// 
    /// WARNING: Do not store plaintext sensitive data (passwords, tokens) here.
    /// Use the [Encrypted] attribute for values containing PII or secrets.
    /// </remarks>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the module that owns this setting.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 100 characters. Usually in format "dotnetcloud.modulename".
    /// Examples: "dotnetcloud.core", "dotnetcloud.ui", "dotnetcloud.notifications".
    /// Enables namespace organization of settings across multiple modules at the user level.
    /// Combined with Key and UserId, must be unique.
    /// </remarks>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this setting was last updated.
    /// </summary>
    /// <remarks>
    /// Automatically set to UTC now when created or modified.
    /// Helps track when settings were last changed for audit purposes.
    /// Useful for detecting stale cached values and tracking user preference changes.
    /// </remarks>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the optional description explaining the purpose of this setting.
    /// </summary>
    /// <remarks>
    /// Optional. Maximum 500 characters. Provides context for the setting.
    /// Helps document what values are valid and the impact of changing this setting.
    /// Example: "User's preferred UI theme (light, dark, auto)"
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this setting contains sensitive data that should be encrypted.
    /// </summary>
    /// <remarks>
    /// Optional. Default false. When true, indicates that the Value property contains sensitive data
    /// and should be encrypted at rest in the database.
    /// 
    /// Sensitive data includes:
    /// - Personally Identifiable Information (PII)
    /// - Authentication tokens or credentials
    /// - API keys or secrets
    /// - Payment information
    /// - Health or status information
    /// 
    /// The application layer (data protection provider, EF Core value converter, or database encryption)
    /// is responsible for encryption/decryption based on this flag.
    /// </remarks>
    public bool IsEncrypted { get; set; } = false;
}
