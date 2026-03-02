namespace DotNetCloud.Core.Data.Entities.Settings;

/// <summary>
/// Represents an organization-scoped setting that applies to a specific organization and its members.
/// </summary>
/// <remarks>
/// Organization settings are configuration values that control behavior for a specific organization.
/// Each organization can override system settings with their own values, enabling multi-tenancy configuration.
/// 
/// Examples of organization settings include:
/// - "core.MaxTeamSize" - Maximum number of teams in this organization
/// - "storage.QuotaGB" - Total storage quota for this organization
/// - "notifications.EmailDomain" - Organization-specific email domain for notifications
/// - "branding.PrimaryColor" - Organization-specific branding color
/// 
/// Organization settings take precedence over system settings when present.
/// If an organization setting is not defined, the system setting or default value is used.
/// </remarks>
public class OrganizationSetting
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
    /// Gets or sets the organization ID this setting belongs to.
    /// </summary>
    /// <remarks>
    /// Required. Foreign key reference to the Organization entity.
    /// Establishes the organization scope for this setting.
    /// Cannot be null; each setting is tied to exactly one organization.
    /// </remarks>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the setting key that uniquely identifies this setting within the organization.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 200 characters. Uses hierarchical naming convention with dots.
    /// Examples: "MaxTeamSize", "StorageQuotaGB", "Email.Domain", "Branding.PrimaryColor".
    /// Combined with Module and OrganizationId, must be unique.
    /// Keys are case-sensitive.
    /// </remarks>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the setting value as a JSON-serializable string.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 10,000 characters. Stores the actual setting value.
    /// Values are JSON-serializable to support complex types:
    /// - Simple types: "1000", "true", "organization@example.com"
    /// - Complex types: "{\"enabled\": true, \"domains\": [\"example.com\", \"mail.example.com\"]}"
    /// 
    /// Consumers should deserialize based on expected type.
    /// </remarks>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the module that owns this setting.
    /// </summary>
    /// <remarks>
    /// Required. Maximum 100 characters. Usually in format "dotnetcloud.modulename".
    /// Examples: "dotnetcloud.core", "dotnetcloud.files", "dotnetcloud.notifications".
    /// Enables namespace organization of settings across multiple modules at the organization level.
    /// Combined with Key and OrganizationId, must be unique.
    /// </remarks>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this setting was last updated.
    /// </summary>
    /// <remarks>
    /// Automatically set to UTC now when created or modified.
    /// Helps track when settings were last changed for audit purposes.
    /// Useful for detecting stale cached values and tracking configuration changes.
    /// </remarks>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the optional description explaining the purpose of this setting.
    /// </summary>
    /// <remarks>
    /// Optional. Maximum 500 characters. Provides context for organization administrators.
    /// Helps explain what values are valid and the impact of changing this setting.
    /// Example: "Maximum number of teams allowed in this organization"
    /// </remarks>
    public string? Description { get; set; }
}
