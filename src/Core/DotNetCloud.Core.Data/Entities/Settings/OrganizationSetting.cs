namespace DotNetCloud.Core.Data.Entities.Settings;

/// <summary>
/// Represents an organization-scoped setting.
/// Placeholder entity for Phase 0.2.5 - Settings Models.
/// </summary>
/// <remarks>
/// This entity will be fully implemented in phase-0.2.5.
/// Temporary placeholder to satisfy Organization navigation property.
/// </remarks>
public class OrganizationSetting
{
    /// <summary>
    /// Gets or sets the unique identifier for the setting.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the organization ID this setting belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the setting key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the setting value (JSON-serializable).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the module that owns this setting.
    /// </summary>
    public string Module { get; set; } = string.Empty;
}
