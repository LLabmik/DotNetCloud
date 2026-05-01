namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Data transfer object for module information.
/// </summary>
public class ModuleDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the module (e.g., "dotnetcloud.files").
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module version.
    /// </summary>
    public string Version { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this module is architecturally required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the module status (Enabled, Disabled, UpdateAvailable).
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date and time the module was installed.
    /// </summary>
    public DateTime InstalledAt { get; set; }

    /// <summary>
    /// Gets or sets the required capabilities for this module.
    /// </summary>
    public ICollection<string> RequiredCapabilities { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the events published by this module.
    /// </summary>
    public ICollection<string> PublishedEvents { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the events subscribed to by this module.
    /// </summary>
    public ICollection<string> SubscribedEvents { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the granted capabilities for this module.
    /// </summary>
    public ICollection<ModuleCapabilityGrantDto> GrantedCapabilities { get; set; } = new List<ModuleCapabilityGrantDto>();
}

/// <summary>
/// Data transfer object for creating/registering a module.
/// </summary>
public class CreateModuleDto
{
    /// <summary>
    /// Gets or sets the module ID (required, e.g., "dotnetcloud.files").
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module name (required).
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module version (required).
    /// </summary>
    public string Version { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the required capabilities (optional).
    /// </summary>
    public ICollection<string> RequiredCapabilities { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the events published by this module (optional).
    /// </summary>
    public ICollection<string> PublishedEvents { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the events subscribed to by this module (optional).
    /// </summary>
    public ICollection<string> SubscribedEvents { get; set; } = new List<string>();
}

/// <summary>
/// Data transfer object for module capability grant information.
/// </summary>
public class ModuleCapabilityGrantDto
{
    /// <summary>
    /// Gets or sets the module ID.
    /// </summary>
    public string ModuleId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the capability name.
    /// </summary>
    public string CapabilityName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date and time the capability was granted.
    /// </summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>
    /// Gets or sets the ID of the admin who granted this capability (if applicable).
    /// </summary>
    public Guid? GrantedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the admin who granted this capability (if applicable).
    /// </summary>
    public string? GrantedByUserDisplayName { get; set; }
}

/// <summary>
/// Data transfer object for granting a capability to a module.
/// </summary>
public class GrantModuleCapabilityDto
{
    /// <summary>
    /// Gets or sets the capability name to grant (required).
    /// </summary>
    public string CapabilityName { get; set; } = null!;
}
