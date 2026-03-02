namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Data transfer object for user device information.
/// </summary>
public class UserDeviceDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the device.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the device name (e.g., "Windows Laptop", "iPhone").
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the device type (Desktop, Mobile, Tablet, etc.).
    /// </summary>
    public string DeviceType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the device identifier (e.g., UUID, IMEI).
    /// </summary>
    public string? DeviceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the push notification token for this device.
    /// </summary>
    public string? PushToken { get; set; }

    /// <summary>
    /// Gets or sets the date and time the device was last seen.
    /// </summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time the device was registered.
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the operating system name and version.
    /// </summary>
    public string? OsVersion { get; set; }

    /// <summary>
    /// Gets or sets the application version running on the device.
    /// </summary>
    public string? AppVersion { get; set; }

    /// <summary>
    /// Gets or sets the IP address the device last connected from.
    /// </summary>
    public string? LastIpAddress { get; set; }
}

/// <summary>
/// Data transfer object for registering a new user device.
/// </summary>
public class RegisterUserDeviceDto
{
    /// <summary>
    /// Gets or sets the device name (required, e.g., "Windows Laptop").
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the device type (required, e.g., "Desktop", "Mobile", "Tablet").
    /// </summary>
    public string DeviceType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the device identifier (optional, e.g., UUID, IMEI).
    /// </summary>
    public string? DeviceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the push notification token (optional).
    /// </summary>
    public string? PushToken { get; set; }

    /// <summary>
    /// Gets or sets the operating system name and version (optional).
    /// </summary>
    public string? OsVersion { get; set; }

    /// <summary>
    /// Gets or sets the application version (optional).
    /// </summary>
    public string? AppVersion { get; set; }
}

/// <summary>
/// Data transfer object for updating user device information.
/// </summary>
public class UpdateUserDeviceDto
{
    /// <summary>
    /// Gets or sets the device name (optional).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the push notification token (optional).
    /// </summary>
    public string? PushToken { get; set; }

    /// <summary>
    /// Gets or sets the operating system name and version (optional).
    /// </summary>
    public string? OsVersion { get; set; }

    /// <summary>
    /// Gets or sets the application version (optional).
    /// </summary>
    public string? AppVersion { get; set; }
}
