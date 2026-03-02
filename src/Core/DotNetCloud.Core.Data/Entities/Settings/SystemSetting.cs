namespace DotNetCloud.Core.Data.Entities.Settings;

/// <summary>
/// Represents a system-wide setting that applies across the entire DotNetCloud instance.
/// </summary>
/// <remarks>
/// System settings are global configuration values that control platform-wide behavior.
/// Each setting is scoped by both Module and Key, with a composite primary key (Module, Key).
/// 
/// Examples of system settings include:
/// - "core.MaxUploadSize" - Maximum file upload size for all users
/// - "core.SessionTimeout" - Global session timeout duration
/// - "core.EnableRegistration" - Whether new user registration is allowed
/// - "notifications.EmailEnabled" - Whether email notifications are enabled
/// 
/// System settings are typically configured during initial setup via the CLI wizard
/// and can be modified by administrators through the admin API endpoints.
/// </remarks>
public class SystemSetting
{
    /// <summary>
    /// Gets or sets the module identifier that owns this setting.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 100 characters. Usually in format "dotnetcloud.modulename" (e.g., "dotnetcloud.core", "dotnetcloud.files").
    /// This forms part of the composite primary key along with Key.
    /// Enables namespace organization of settings across multiple modules.
    /// </remarks>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the setting key that uniquely identifies this setting within the module.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 200 characters. Uses hierarchical naming convention with dots.
    /// Examples: "MaxUploadSize", "SessionTimeout", "EnableRegistration", "Email.Provider".
    /// This forms part of the composite primary key along with Module.
    /// Keys are case-sensitive and must be unique per module.
    /// </remarks>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the setting value as a JSON-serializable string.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 10,000 characters. Stores the actual setting value.
    /// Values are JSON-serializable to support complex types:
    /// - Simple types: "true", "1024", "3600", "example@mail.com"
    /// - Complex types: "{\"enabled\": true, \"retryCount\": 3}"
    /// 
    /// Consumers should deserialize based on expected type. For sensitive values,
    /// consider using UserSetting or OrganizationSetting which offer encryption options.
    /// </remarks>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this setting was last updated.
    /// </summary>
    /// <remarks>
    /// Automatically set to UTC now when created or modified.
    /// Helps track when settings were last changed for audit purposes.
    /// Useful for detecting stale cached values.
    /// </remarks>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the optional description explaining the purpose of this setting.
    /// </summary>
    /// <remarks>
    /// Optional. Maximum 500 characters. Provides context for administrators.
    /// Helps explain what values are valid and the impact of changing this setting.
    /// Example: "Maximum file size in bytes that can be uploaded (default: 5GB)"
    /// </remarks>
    public string? Description { get; set; }
}
