namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Data transfer object for system-level settings.
/// </summary>
public class SystemSettingDto
{
    /// <summary>
    /// Gets or sets the module that owns this setting.
    /// </summary>
    public string Module { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting key.
    /// </summary>
    public string Key { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting value (JSON serializable).
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the setting is sensitive (should be masked in logs).
    /// </summary>
    public bool IsSensitive { get; set; }
}

/// <summary>
/// Data transfer object for organization-level settings.
/// </summary>
public class OrganizationSettingDto
{
    /// <summary>
    /// Gets or sets the organization ID.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the module that owns this setting.
    /// </summary>
    public string Module { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting key.
    /// </summary>
    public string Key { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting value (JSON serializable).
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the setting is sensitive (should be masked in logs).
    /// </summary>
    public bool IsSensitive { get; set; }
}

/// <summary>
/// Data transfer object for user-level settings.
/// </summary>
public class UserSettingDto
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the module that owns this setting.
    /// </summary>
    public string Module { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting key.
    /// </summary>
    public string Key { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting value (JSON serializable, encrypted for sensitive data).
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the setting is sensitive (should be masked in logs).
    /// </summary>
    public bool IsSensitive { get; set; }
}

/// <summary>
/// Data transfer object for creating/updating a system setting.
/// </summary>
public class UpsertSystemSettingDto
{
    /// <summary>
    /// Gets or sets the setting value (required, JSON serializable).
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the setting is sensitive (optional).
    /// </summary>
    public bool IsSensitive { get; set; }
}

/// <summary>
/// Data transfer object for creating/updating an organization setting.
/// </summary>
public class UpsertOrganizationSettingDto
{
    /// <summary>
    /// Gets or sets the setting value (required, JSON serializable).
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the setting is sensitive (optional).
    /// </summary>
    public bool IsSensitive { get; set; }
}

/// <summary>
/// Data transfer object for creating/updating a user setting.
/// </summary>
public class UpsertUserSettingDto
{
    /// <summary>
    /// Gets or sets the setting value (required, JSON serializable).
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the setting is sensitive (optional).
    /// </summary>
    public bool IsSensitive { get; set; }
}

/// <summary>
/// Data transfer object for bulk settings.
/// </summary>
public class SettingsBulkDto
{
    /// <summary>
    /// Gets or sets the module.
    /// </summary>
    public string Module { get; set; } = null!;

    /// <summary>
    /// Gets or sets the collection of settings.
    /// </summary>
    public ICollection<SettingKeyValueDto> Settings { get; set; } = new List<SettingKeyValueDto>();
}

/// <summary>
/// Data transfer object for a single setting key-value pair.
/// </summary>
public class SettingKeyValueDto
{
    /// <summary>
    /// Gets or sets the setting key.
    /// </summary>
    public string Key { get; set; } = null!;

    /// <summary>
    /// Gets or sets the setting value.
    /// </summary>
    public string Value { get; set; } = null!;
}
